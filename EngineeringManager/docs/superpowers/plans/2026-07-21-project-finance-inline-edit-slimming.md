# 项目业务明细快捷编辑与财务入口精简 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with review checkpoints.

**Goal:** 删除项目详细编辑页，让项目详情快捷编辑成为日常唯一入口，同时精简收款、发票、付款、中央账本和施工流转操作，并为五类业务记录提供单附件替换。

**Architecture:** 保留现有 Razor Pages、应用服务和中央账本事务边界。项目详情页继续负责展示和快捷编辑；财务服务负责收款自由文本、工程量应收自动分摊、账户流水和审计；附件服务提供通用单附件替换；施工服务把跨项目关系拆成事务化连接/解除命令。中央账本保留历史、查询、对账和管理员修正，不删除历史数据模型。

**Tech Stack:** ASP.NET Core Razor Pages, C#/.NET 10, EF Core/SQL Server, xUnit + FluentAssertions, PowerShell 7, 现有 CSS/JS 工作台组件。

---

## 文件地图

- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`：五个标签页的显示、快捷编辑、附件控件和折叠新增表单。
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`：项目详情 POST handler、权限、输入模型和附件下载/删除/上传。
- `src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`：项目财务/施工工作区查询和收款方式文本映射。
- `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`：应收、收款、付款写入、自动分摊、账户流水和审计。
- `src/EngineeringManager.Infrastructure/Finance/FinancePostingService.cs`：工程量同步应收金额。
- `src/EngineeringManager.Domain/Finance/CentralLedgerEnums.cs`：新增项目收款来源枚举值。
- `src/EngineeringManager.Application/Finance/FinanceDtos.cs`、`src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`：收款方式由枚举改为文本，付款方式保持原枚举。
- `src/EngineeringManager.Application/Projects/IProjectRecordAttachmentService.cs`、`src/EngineeringManager.Infrastructure/Projects/ProjectRecordAttachmentService.cs`：通用单附件替换。
- `src/EngineeringManager.Web/Pages/Projects/Records/*`：删除详细编辑页及仅供其使用的 partial/view model。
- `src/EngineeringManager.Infrastructure/Projects/ProjectConstructionService.cs`、`src/EngineeringManager.Application/Projects/ProjectConstructionDtos.cs`：施工流转连接/解除命令。
- `src/EngineeringManager.Web/Pages/Ledger/*`、`src/EngineeringManager.Web/Pages/Finance/*`：移除项目场景人工应收和关联应收入口，保留全局查询和管理员修正。
- `tests/EngineeringManager.Tests/Application/*`、`tests/EngineeringManager.Tests/Web/*`：应用服务和 Razor 页面回归覆盖。

### Task 1: 建立失败测试和基线

**Files:**
- Modify: `tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Application/ProjectRecordAttachmentServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Application/ProjectConstructionServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`

- [ ] **Step 1: 记录现有测试和工作区状态**

Run from `D:\AI\TZM-project\EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --no-restore
```

Expected: 当前基线测试结果，任何已有失败记录在实施日志中，不修改无关失败。

- [ ] **Step 2: 添加收款自动分摊、自由文本和项目来源的失败测试**

覆盖以下行为：

```csharp
[Fact]
public async Task Collection_without_related_receivable_auto_allocates_in_due_date_order() { /* assert allocation order and project summary */ }

[Fact]
public async Task Collection_over_receivable_amount_keeps_project_source_and_unallocated_balance() { /* assert SourceId, allocations and remaining amount */ }

[Fact]
public async Task Collection_payment_method_accepts_custom_text() { /* assert arbitrary text survives create/update/workspace mapping */ }
```

- [ ] **Step 3: 添加单附件替换和施工关系失败测试**

```csharp
[Fact]
public async Task Non_quantity_record_attachment_replacement_keeps_one_active_attachment() { /* assert old soft-deleted */ }

[Fact]
public async Task Linking_next_construction_record_updates_both_sides_atomically() { /* assert PreviousRecordId/NextRecordId */ }

[Fact]
public async Task Construction_subject_switch_is_rejected_for_existing_record() { /* assert identity remains immutable */ }
```

- [ ] **Step 4: 添加页面契约失败断言**

断言项目详情源码不包含 `Projects/Records/Edit`、应收记录表格和“关联应收”，发票表头不包含“方向”，五类记录均包含附件控件；旧路由页面测试期望 404。

- [ ] **Step 5: 运行新增测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~FinanceLedgerServiceTests|FullyQualifiedName~ProjectRecordAttachmentServiceTests|FullyQualifiedName~ProjectConstructionServiceTests|FullyQualifiedName~ProjectAuthorizationTests"
```

Expected: 新增断言失败，且失败原因指向尚未实现的字段/服务，而不是测试编译错误。

### Task 2: 收款文本模型、项目来源与自动分摊

**Files:**
- Modify: `src/EngineeringManager.Domain/Finance/CentralLedgerEnums.cs`
- Modify: `src/EngineeringManager.Application/Finance/FinanceDtos.cs`
- Modify: `src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/FinancePostingService.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Test: `tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`

- [ ] **Step 1: 将收款请求和项目工作区的收款方式改为 `string?`**

只修改收款相关 DTO、请求和映射；付款请求及 `ProjectPaymentItemDto.PaymentMethod` 保持现有 `PaymentMethod` 枚举。旧数据使用其现有字符串原样显示，空值显示为 `-`。

- [ ] **Step 2: 新增项目收款来源枚举值**

在 `LedgerSourceType` 末尾增加 `ProjectCollection`，避免改变已有整数值。项目详情创建收款时写入 `SourceType = ProjectCollection` 和 `SourceId = projectId`，用于没有分摊余额时仍能归属项目。

- [ ] **Step 3: 实现自动分摊算法**

在财务服务中按当前项目有效工程量应收排序：`DueDate ?? DateOnly.MaxValue`、业务日期、主键。创建和更新收款都先清理该收款原有分摊，再按剩余应收金额逐条分配；超出部分不创建负分摊，保留在收款金额与分摊合计的差额中。

- [ ] **Step 4: 更新项目查询和汇总**

项目工作区收款查询加入 `SourceType == ProjectCollection && SourceId == projectId`，同时保留现有 allocation 查询，避免同一记录重复计数。项目已收汇总继续以分摊金额为准；未分摊金额由中央账本查询显示。

- [ ] **Step 5: 保持工程量应收同步为唯一应收来源**

保留 `FinancePostingService.SynchronizeProjectQuantityReceivablesAsync` 的工程量来源约束；删除项目页面新增人工应收的调用路径。工程量新增、修改和项目阶段变化仍触发同步。

- [ ] **Step 6: 运行财务测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~FinanceLedgerServiceTests|FullyQualifiedName~ProjectWorkspaceServiceTests"
```

Expected: Task 1 的收款相关测试 PASS。

### Task 3: 通用单附件替换和项目详情附件数据流

**Files:**
- Modify: `src/EngineeringManager.Application/Projects/IProjectRecordAttachmentService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectRecordAttachmentService.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/js/components/attachment-picker.js`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Test: `tests/EngineeringManager.Tests/Application/ProjectRecordAttachmentServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] **Step 1: 将 `ReplaceQuantityAsync` 提炼为通用 `ReplaceAsync`**

保留 `ReplaceQuantityAsync` 的兼容调用或同步更新调用方；通用方法必须校验记录类型、项目归属、文件大小和文件名，并在文件存储成功后以 Serializable 事务软删除旧附件、写入新附件。数据库保存失败时删除新文件并保留旧附件。

- [ ] **Step 2: 为详情页加载各类附件字典**

按 `ProjectRecordAttachmentType` 加载收款、发票、应付、付款、施工记录附件。由于每条只允许一个有效附件，页面只展示最新有效附件；不在 Razor 中执行删除旧附件的组合操作。

- [ ] **Step 3: 增加详情页附件 handler**

新增统一的上传、下载和删除 handler，参数必须包含项目 ID、记录类型、记录 ID、附件 ID（删除时）并复用现有 `CanManage` / `CanManageFinance` 权限。上传成功重定向回对应标签页和记录锚点。

- [ ] **Step 4: 在收款、发票、应付、付款、施工表格增加紧凑附件列**

每行显示“上传/替换”“预览”“删除”三个状态化操作；保持现有业务字段顺序，不把中央账本分摊字段塞进表格。新增表单支持创建后上传附件，附件失败时保留业务记录并显示明确提示。

- [ ] **Step 5: 运行附件测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProjectRecordAttachmentServiceTests|FullyQualifiedName~ProjectAuthorizationTests"
```

Expected: 五类业务记录的单附件首次上传、替换、删除和失败回滚 PASS。

### Task 4: 项目详情快捷编辑和字段精简

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `src/EngineeringManager.Web/wwwroot/js/site.js` 或当前 inline-edit 脚本文件
- Test: `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] **Step 1: 重构收款新增表单**

删除业务类型、关联应收和新增应收分支，表单只创建实际收款；收款方式使用 `<input>` 文本框，标签改为“收款账户”“收款金额”“收款方式”。

- [ ] **Step 2: 扩展收款快捷编辑字段**

将合同、签约公司、付款方、收款账户、日期、金额、自由文本方式、备注纳入逐行表单，去除不再存在的关联应收隐藏字段。普通收款调用新的文本收款更新请求。

- [ ] **Step 3: 精简应收区域**

删除应收记录表格和应收编辑行，只显示工程量自动应收汇总、已收、未收及收款记录；保留收款关联所需的后台自动分摊，不在页面显示选择器。

- [ ] **Step 4: 精简发票字段**

删除方向表头、方向单元格和编辑控件；保留销项表格现有字段，并把合同、开票公司、往来单位、税率、号码、日期和金额字段都接入快捷编辑。

- [ ] **Step 5: 保持付款表格字段并接入附件**

应付和付款的业务字段不扩展；应付、实际付款分别使用 `Settlement` / `Cash` 附件类型。工资代发付款行保留“查看来源批次”，禁止普通财务行 handler 修改派生工资数据。

- [ ] **Step 6: 调整桌面紧凑布局**

沿用工程量新增的紧凑网格、固定表格宽度和横向滚动；为附件操作设置稳定列宽，为备注保留剩余空间，桌面 1440px 及 1280px 不发生按钮、文字重叠。

- [ ] **Step 7: 运行页面测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ProjectAuthorizationTests"
```

Expected: 页面契约和权限测试 PASS，所有标签页不再包含详细编辑入口或关联应收控件。

### Task 5: 施工流转专用操作

**Files:**
- Modify: `src/EngineeringManager.Application/Projects/ProjectConstructionDtos.cs`
- Modify: `src/EngineeringManager.Application/Projects/IProjectConstructionService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectConstructionService.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Test: `tests/EngineeringManager.Tests/Application/ProjectConstructionServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`

- [ ] **Step 1: 锁定正式记录的类型和主体**

在 `SaveAsync` 中，当 `request.Id` 非空时，若 `RecordType`、`EquipmentId` 或 `CrewBusinessPartnerId` 与现有记录不同，返回“正式施工记录不能直接切换设备或班组，请新建正确记录”。新增记录仍允许选择类型和主体。

- [ ] **Step 2: 增加事务化连接命令**

新增 `LinkNextAsync`、`LinkPreviousAsync`、`UnlinkAsync` 服务方法，输入当前记录 ID、目标项目/记录、两个并发戳和修改原因；方法在 Serializable 事务中重新读取双方记录、检查主体、循环和现有连接，再成对更新关系。

- [ ] **Step 3: 保证幂等创建后续草稿**

连接目标项目时先按主体、项目和 `PreviousRecordId` 查找既有草稿；找到则复用，找不到才创建。任何保存失败都回滚当前记录、目标草稿和附件关联。

- [ ] **Step 4: 替换快捷编辑中的跨项目字段按钮**

移除普通行内直接编辑“调入/调出项目”和“自动连接下个项目”按钮，增加“连接上一条”“关联后续项目”“解除流转”三个受控动作及紧凑确认表单。设备/班组主体显示为只读。

- [ ] **Step 5: 运行施工测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProjectConstructionServiceTests|FullyQualifiedName~ConstructionCrewPageTests"
```

Expected: 双向关系、循环保护、幂等草稿、并发冲突和主体锁定测试 PASS。

### Task 6: 删除详细编辑页并精简中央账本入口

**Files:**
- Delete: `src/EngineeringManager.Web/Pages/Projects/Records/Edit.cshtml`
- Delete: `src/EngineeringManager.Web/Pages/Projects/Records/Edit.cshtml.cs`
- Delete if unused: `src/EngineeringManager.Web/Pages/Projects/Records/_FinanceRecordEditor.cshtml`
- Delete if unused: `src/EngineeringManager.Web/Pages/Projects/Records/FinanceRecordEditorViewModel.cs`
- Delete if unused: `src/EngineeringManager.Web/Pages/Projects/Records/_ConstructionRecordEditor.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/FinanceAuthorizationTests.cs`

- [ ] **Step 1: 清理所有项目详情详细编辑链接**

用 `rg -n "Projects/Records/Edit|详细编辑|进入详细编辑|在中央账本详细录入" src tests` 找出入口，删除项目详情和项目创建表单中的跳转；保留全局中央账本管理员查询/修正入口，不保留项目页快捷跳转。

- [ ] **Step 2: 删除详细编辑页面及仅页面 partial**

确认没有其他页面引用后删除文件；对旧 URL 添加或保留 404 测试，不添加重定向，避免用户继续依赖旧页面。

- [ ] **Step 3: 精简项目相关中央账本输入**

禁止项目场景人工新增应收和关联应收选择；保留发票、应付、付款的全局记录、审计、对账和管理员分摊修正。不得删除既有 `FinanceSettlement`、`FinanceCashEntry`、allocation 或历史迁移数据。

- [ ] **Step 4: 更新权限和页面测试**

断言查询用户不能看到创建/上传/流转操作，项目经理和财务角色能在项目详情快捷编辑，旧详细编辑路由返回 404，中央账本查询仍可访问。

### Task 7: 构建、完整测试和桌面端验收

**Files:**
- Modify only if failures reveal a direct regression in files above.

- [ ] **Step 1: 运行格式/编译检查**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release --no-restore
```

Expected: Razor、C# 和资源编译成功。

- [ ] **Step 2: 运行完整 Release 测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release --no-build
```

Expected: 全部测试通过；若有既有失败，区分并记录，不扩大修改范围。

- [ ] **Step 3: 启动开发站点并做桌面端浏览器检查**

只验证桌面视口（至少 1440×900 和 1280×800）：

1. 工程量、收款、发票、付款、施工标签页均无详细编辑入口。
2. 收款新增/快捷编辑支持自由文本收款方式，页面无关联应收控件。
3. 发票无方向列，往来单位只显示一次。
4. 应付、付款字段保持现有明细，附件可上传、预览、替换和删除。
5. 施工普通编辑不改变主体；连接、解除和后续项目操作可完成且无横向重叠。
6. 表格备注、附件按钮和操作列无遮挡；不做手机端测试。

- [ ] **Step 4: 记录验证结果**

在最终交付中列出构建、完整测试和两个桌面视口的实际命令与结果；不得声称完成未执行的手机端验证。
