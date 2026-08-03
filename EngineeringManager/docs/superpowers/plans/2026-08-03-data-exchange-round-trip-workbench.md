# 数据往返导入导出工作台实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with review checkpoints.

**Goal:** 将现有混合式数据交换页面改造成以“导出 Excel → 补充/修改 → 预览 → 整批回导”为核心的统一工作台。

**Architecture:** 保留现有 `IExportService`、`IImportService`、项目工作簿服务作为业务适配器，在其上增加统一的数据集目录、往返文件元数据和任务查询层。Web 层拆成导出、导入、任务记录三个 Razor Pages 视图；旧 `/DataExchange` 入口重定向到导出视图。第一阶段只改造稳定数据集和现有项目工作簿契约，不直接反射暴露 EF 表，也不重写统一财务模型。

**Tech Stack:** ASP.NET Core 10 Razor Pages、C#、EF Core SQL Server/SQLite 测试夹具、现有 `SimpleXlsxWorkbook`、PowerShell 7、xUnit、FluentAssertions。

---

## 任务 1：锁定往返导入导出契约和持久化元数据

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/DataExchange/DataExchangeEnums.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/ImportDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/ExportDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ImportBatch.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/DataExchangeTask.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs:462-559`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/<generated>_DataExchangeRoundTripMetadata.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/DataExchangeRoundTripContractTests.cs`

- [ ] **Step 1: 写失败测试，固定来源类型、版本、来源导出批次和文件摘要契约。**

测试至少覆盖：`ImportSourceType.SystemExport`、`BlankTemplate`、`ExternalWorkbook`、`ProjectWorkbook`；导入批次能保存 `SourceExportTaskId`、`DatasetVersion`、`SourceSha256`；同一批次的摘要与原始字节 SHA-256 一致。

- [ ] **Step 2: 运行契约测试，确认当前模型缺少字段。**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~DataExchangeRoundTripContractTests'
```

Expected: FAIL，原因是来源类型或批次元数据尚未实现。

- [ ] **Step 3: 增加最小领域和应用契约。**

新增 `ImportSourceType`；在导入 DTO 中增加来源类型、来源导出任务 ID、数据集版本和源文件摘要；在导出 DTO 中增加数据集版本和往返批次标识。保留现有构造函数兼容性，使用可选参数避免一次性破坏现有页面和测试。

建议的核心形状：

```csharp
public enum ImportSourceType
{
    SystemExport = 1,
    BlankTemplate = 2,
    ExternalWorkbook = 3,
    ProjectWorkbook = 4
}

public sealed record ImportSourceMetadata(
    ImportSourceType SourceType,
    Guid? SourceExportTaskId,
    string DatasetVersion,
    string SourceSha256);
```

- [ ] **Step 4: 将元数据持久化到 `ImportBatch`/`DataExchangeTask` 并配置索引。**

`ImportBatch` 至少增加 `SourceType`、`SourceExportTaskId`、`DatasetVersion`、`SourceSha256`、`ErrorReportContent`；`DataExchangeTask` 增加 `DatasetVersion` 和 `SourcePage`。文件名、摘要、用户和创建时间保持现有字段，不重复创建第二套任务表。

- [ ] **Step 5: 生成并检查 EF 迁移。**

Run:

```powershell
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath D:\AI\TZM-project\EngineeringManager
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add DataExchangeRoundTripMetadata --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
```

检查迁移只新增数据交换元数据列和索引，不修改业务表；测试数据库更新必须单独执行，不在应用启动时自动迁移。

- [ ] **Step 6: 运行契约、数据库模型和现有导入导出测试。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~DataExchangeRoundTripContractTests|FullyQualifiedName~StandardImportTests|FullyQualifiedName~ModuleExportTests'
```

- [ ] **Step 7: 在获得 Git 明确授权后提交本任务。**

提交内容只包括契约、模型配置、迁移和对应测试，不包含页面重构。

## 任务 2：实现标准 Excel 往返文件和控制列

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/DataExchange/RoundTripWorkbookDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/RoundTripWorkbookBuilder.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxWorkbook.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs:376-547`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/SimpleXlsxWorkbookTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ModuleExportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs`

- [ ] **Step 1: 添加失败测试，验证默认 Excel 结构和隐藏控制列。**

断言标准导出包含 `目录`、`数据说明` 和业务工作表；业务工作表包含隐藏并保护的 `_record_id`、`_business_key`、`_row_version`、`_dataset_key`、`_dataset_version`、`_export_batch_id`；普通字段仍按用户选择顺序显示。

- [ ] **Step 2: 运行新增测试确认当前导出结构不满足要求。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~RoundTripWorkbook|FullyQualifiedName~SimpleXlsxWorkbookTests'
```

- [ ] **Step 3: 抽取 `RoundTripWorkbookBuilder`。**

输入数据集定义、字段选择、行数据和批次元数据，输出单 Excel。复用 `SimpleXlsxWorkbook` 已有的 `HiddenColumnIndexes` 和 `ProtectSheet`，控制列隐藏但不锁定用户可编辑字段；说明页写明空白不修改、`【清空】` 标记、并发冲突和错误报告下载方式。

- [ ] **Step 4: 改造普通导出和项目工作簿导出调用统一构建器。**

保留列表页现有导出入口和权限判断；普通模块默认 Excel，多模块合并到 `目录` 和多个工作表。附件选项继续由现有 ZIP 逻辑处理，并在 `manifest.json` 写入数据集版本、导出批次和文件摘要。

- [ ] **Step 5: 运行模块、项目工作簿和往返文件测试。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~ModuleExportTests|FullyQualifiedName~ProjectWorkbookExportTests|FullyQualifiedName~SimpleXlsxWorkbookTests'
```

- [ ] **Step 6: 在获得 Git 明确授权后提交本任务。**

提交内容只包括工作簿构建器、导出适配和测试。

## 任务 3：实现回导识别、空白不修改和并发冲突

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/DataExchange/ImportRowIdentity.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ImportRoundTripMetadataReader.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs:348-470`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxReader.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/StandardImportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`

- [ ] **Step 1: 写失败测试，覆盖三类往返场景。**

1. 导出一条员工或项目记录，修改一个字段、保留其他字段空白，回导后只修改目标字段；
2. 导出后在数据库中改变并发版本，再回导，预览必须报告冲突且确认不写入任何行；
3. 在导出工作表末尾追加一行，控制列为空时按新增模式校验并创建新记录。

- [ ] **Step 2: 运行测试确认当前导入器未完成控制列和版本检查。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~StandardImportTests|FullyQualifiedName~ProjectWorkbookImportTests'
```

- [ ] **Step 3: 解析标准元数据并确定来源类型。**

先验证 `_dataset_key`、`_dataset_version`、`_export_batch_id` 和 SHA-256；全部存在且一致时走系统导出路径。缺少控制列时保留现有任意 Excel 映射路径，不把普通旧文件误判为系统导出。

- [ ] **Step 4: 实现行定位和版本检查。**

优先 `_record_id`，其次稳定业务编号；控制列被篡改、记录不存在或数据集不匹配时生成行级错误。`_row_version` 与当前版本不一致时生成“并发冲突”，并让整批预览不可确认。

- [ ] **Step 5: 实现空白与显式清空语义。**

系统导出回导时空白值跳过赋值；只有可空、可编辑字段接受精确的 `【清空】` 标记；必填、计算、敏感只读和关联控制字段拒绝清空。预览中显示“未变化/清空/更新”数量。

- [ ] **Step 6: 保持整批原子写入。**

预览阶段保存原始字节、元数据和错误，不修改业务表；确认阶段重新解析并重新检查版本，然后在现有事务内按模块依赖顺序应用。任何错误时 `ConfirmAsync` 必须拒绝，现有 `PreviewReturnsRowErrorsAndDoesNotPartiallyImport` 继续通过。

- [ ] **Step 7: 运行完整导入回归。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~StandardImportTests|FullyQualifiedName~ProjectWorkbookImportTests|FullyQualifiedName~ProjectWorkbookCentralLedgerTests'
```

- [ ] **Step 8: 在获得 Git 明确授权后提交本任务。**

## 任务 4：统一导入/导出任务查询和错误报告下载

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/DataExchange/DataExchangeTaskDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/DataExchange/IDataExchangeTaskService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/DataExchangeTaskService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/IExportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/IImportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/DataExchangeTaskServiceTests.cs`

- [ ] **Step 1: 写失败测试，验证导入和导出记录统一按最新时间排序。**

测试覆盖方向、数据集、状态、用户权限、20/50/100 页大小、错误报告是否可下载，以及导入批次与来源导出任务的关联。

- [ ] **Step 2: 建立只读任务查询契约。**

应用层返回统一 `DataExchangeTaskListItemDto`，包含方向、批次号、来源页面、模块、行数、新增/更新/未变化/错误数量、状态、文件名、摘要和可用下载项；查询层分别读取现有 `DataExchangeTasks` 与 `ImportBatches`，不让 Razor Page 直接查询 EF。

- [ ] **Step 3: 生成错误报告。**

用 `SimpleXlsxWorkbook` 生成 `导入错误报告_<批次号>.xlsx`，包含原始文件、工作表、行号、来源列、目标字段、原值、错误和建议。下载端点必须按任务所有者或管理员授权。

- [ ] **Step 4: 注册服务并运行应用层测试。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~DataExchangeTaskServiceTests'
```

- [ ] **Step 5: 在获得 Git 明确授权后提交本任务。**

## 任务 5：拆分 Razor Pages 和导入向导

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Export.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Export.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Import.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Import.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Tasks.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Tasks.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/_ExchangeStepper.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/_ExchangeTaskTable.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataExchangeBackupAuthorizationTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataExchangeRoundTripPageTests.cs`

- [ ] **Step 1: 写失败页面测试，固定三个路由和权限。**

测试断言：`/DataExchange/Export`、`/DataExchange/Import`、`/DataExchange/Tasks` 可访问；旧 `/DataExchange` 重定向到导出页；查询用户只能导出和查看任务，管理员才能预览和确认导入。

- [ ] **Step 2: 运行页面测试确认新路由尚不存在。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~DataExchangeRoundTripPageTests|FullyQualifiedName~DataExchangeBackupAuthorizationTests'
```

- [ ] **Step 3: 将现有 `IndexModel` 的加载逻辑拆到三个页面模型。**

导出页只负责范围、字段、模板和生成；导入页只负责来源、上传、映射、预览和确认；任务页只负责筛选、分页和下载。现有项目工作簿筛选作为导出页中的“项目管理工作簿”数据集适配，不再独立占据整页。

- [ ] **Step 4: 实现导入向导的状态回传。**

预览批次 ID、来源导出批次、文件摘要和映射模板通过服务端表单字段保存；确认只接受数据库中状态为“预览就绪”且无错误、无冲突的批次。浏览器刷新不会重复写入。

- [ ] **Step 5: 加入任务记录表格和独立分页。**

默认按 `CreatedAt DESC`；分页选项仅为 20/50/100，保存在任务记录页面自己的查询参数中，不影响项目、员工或其他列表页。

- [ ] **Step 6: 用统一样式重做页面。**

复用现有 `exchange-card`、`data-workbench` 和弹窗令牌；导入步骤使用同一套弹窗和错误提示样式，窄屏下改为单列，不把多个大型表单放在同一首屏。

- [ ] **Step 7: 运行页面和静态资源测试。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~DataExchangeRoundTripPageTests|FullyQualifiedName~DataExchangeBackupAuthorizationTests|FullyQualifiedName~ChineseDisplayTests'
```

- [ ] **Step 8: 在获得 Git 明确授权后提交本任务。**

## 任务 6：接入列表页来源上下文并保留兼容入口

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/ExportDtos.cs`
- Modify: 当前已经提供导出的主要列表页 `Index.cshtml.cs` 和对应 `Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectListExportPageTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataExchangeRoundTripPageTests.cs`

- [ ] **Step 1: 写失败测试，验证快捷导出携带筛选、排序、选中记录和来源页面。**

至少覆盖项目管理页：当前关键词、阶段、负责人、排序方向、选中项目 ID、当前字段顺序进入导出页；导出完成后任务记录可显示来源页面。

- [ ] **Step 2: 增加签名的来源上下文。**

使用现有保存视图/查询参数机制保存上下文，不把未经授权的项目 ID 直接信任为导出范围。服务端再次用当前用户权限过滤，并对字段键、排序键和页面键进行数据集目录校验。

- [ ] **Step 3: 改造项目列表、员工、合作单位、公司、设备和财务等已有导出入口。**

各页面只负责收集当前筛选和列选择，统一跳转到 `/DataExchange/Export`；不在页面复制 Excel 生成逻辑。没有导出能力的列表先只提供“进入导出中心”提示，不伪造空文件。

- [ ] **Step 4: 运行列表页授权和导出测试。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~ProjectListExportPageTests|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~DataExchangeRoundTripPageTests'
```

- [ ] **Step 5: 在获得 Git 明确授权后提交本任务。**

## 任务 7：回归、发布验证和文档收口

**Files:**
- Modify: `EngineeringManager/docs/项目部署手册.md`
- Modify: `EngineeringManager/README.md`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/StandardImportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Performance/RepresentativeDataPerformanceTests.cs`

- [ ] **Step 1: 添加端到端往返测试。**

流程必须覆盖：创建测试记录 → 导出 → 修改一列 → 保留其他列为空 → 回导预览 → 确认 → 断言目标字段变化且其他字段不变；再分别覆盖追加新行、并发冲突、错误整批阻止和含附件 ZIP。

- [ ] **Step 2: 运行数据交换回归集合。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --filter 'FullyQualifiedName~ProjectWorkbook|FullyQualifiedName~StandardImport|FullyQualifiedName~ModuleExport|FullyQualifiedName~DataExchange'
```

Expected: 所有匹配测试通过，且没有新的迁移、权限或静态页面失败。

- [ ] **Step 3: 构建 Release 并重新发布。**

```powershell
$ErrorActionPreference = 'Stop'
dotnet publish EngineeringManager/src/EngineeringManager.Web/EngineeringManager.Web.csproj --configuration Release --no-restore
```

- [ ] **Step 4: 通过浏览器验证三页。**

确认导出页能生成 Excel、导入页能显示预览和错误报告、任务页默认最新在前且分页选项为 20/50/100；同时检查普通导出不强制 ZIP，含附件时才显示 ZIP。

- [ ] **Step 5: 更新部署手册。**

补充三页入口、系统导出文件控制列、空白不修改、并发冲突、整批原子性、错误报告下载和附件 ZIP 规则。明确旧 `/DataExchange` 会进入导出页。

- [ ] **Step 6: 在获得 Git 明确授权后进行最终提交和推送。**

提交前执行 `git diff --check`、检查迁移文件、运行发布验证；没有用户明确授权时只保留工作区变更，不执行 Git 历史或远程操作。

## 计划自检

- 已覆盖规格中的三页信息架构、Excel 控制字段、空白/清空语义、并发冲突、整批原子性、错误报告、任务分页、权限、附件 ZIP、旧入口兼容和分阶段范围。
- 旧的 `IExportService`、`IImportService`、项目工作簿服务继续作为适配器，不引入 EF 反射导出。
- 每个实现任务先写失败测试，再实现，再执行定向测试；数据库迁移和 Git 操作均保留独立确认门槛。
- 未将统一财务模型或后台队列强行混入第一阶段的页面重构；它们在计划任务 7 的验证和后续阶段中明确边界。
