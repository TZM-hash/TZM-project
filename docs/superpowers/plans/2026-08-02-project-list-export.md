# 项目管理页格式导出 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline execution) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让项目管理页导出与当前筛选、排序、选择和列管理状态一致，并默认生成页面格式的“项目清单”Excel。

**Architecture:** 保留现有通用多工作表工作簿协议，在导出请求中增加可选页面格式配置。页面模型提交页面列键；导出器复用项目查询服务的完整匹配顺序，集中生成页面显示值，附件 ZIP 继续走现有归档器。

**Tech Stack:** ASP.NET Core Razor Pages, C#/.NET 8, EF Core, SimpleXlsxWorkbook, xUnit + FluentAssertions, ES modules.

---

### Task 1: 建立页面格式导出契约与失败测试

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/DataExchange/ProjectWorkbookDtos.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs`

- [ ] **Step 1: Add the optional request contract**

在 `ProjectWorkbookExportRequest` 末尾增加可选的 `IReadOnlyCollection<string>? ProjectListColumns = null`。非空时表示项目管理页格式；为空时保持旧的工作簿协议。

- [ ] **Step 2: Write failing behavior tests**

新增测试覆盖：页面格式只生成“项目清单”单表、手动选择保持查询排序、列键顺序/隐藏生效、全量匹配不受 100 条分页限制，并分别验证 Excel-only 与 ZIP 附件路径。

- [ ] **Step 3: Run the focused tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectWorkbookExportTests"
```

Expected: 新增测试失败，因为导出器仍生成旧的多工作表格式、按项目编号重排并忽略 `ProjectListColumns`。

### Task 2: Implement ordered project-list workbook generation

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectListWorkbookExporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs`

- [ ] **Step 1: Preserve query order and selection semantics**

让项目范围解析保留 `SearchProjectsAsync` 返回的 `MatchingProjectIds` 顺序；全量使用完整匹配序列，手动选择使用同序列过滤，不再 `OrderBy(Guid)`。页面格式加载 100 条一页的所有结果，确保完整行数据与页面排序一致。

- [ ] **Step 2: Add page-column mapping and display formatting**

在新导出器中定义页面列白名单、中文表头和映射：项目基本字段、中文枚举、`yyyy-MM-dd` 日期、`N2` 金额、合同/清单数量、收款/付款/开票百分比及应/已/未金额文本、备注截断和页面占位符。按请求列顺序生成表头和行。

- [ ] **Step 3: Route page-format requests and keep legacy path unchanged**

页面格式请求生成单个“项目清单”工作表；附件请求追加“附件清单”并复用现有附件哈希和 `ProjectWorkbookArchive`。通用工作簿请求继续使用现有目录、metadata 和旧字段目录。

- [ ] **Step 4: Run the focused tests and verify GREEN**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectWorkbookExportTests"
```

Expected: 页面格式新增测试和既有附件/权限测试全部通过。

### Task 3: Connect the project page and column manager

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/_ProjectWorkbookExport.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/data-table.js`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`

- [ ] **Step 1: Bind page export columns**

增加 `ExportColumns` 页面绑定属性；项目导出 handler 固定使用页面格式并提交列键。表单输出项目列的隐藏输入，选择框/操作列不进入输入集合，附件开关继续控制 Excel-only 或 ZIP。

- [ ] **Step 2: Synchronize visibility and order**

让 `data-table.js` 找到项目导出表单，在列管理应用时同时禁用隐藏列、按当前列顺序重排 `ExportColumns` 隐藏输入；保留服务端默认列作为无脚本回退。

- [ ] **Step 3: Update web contract tests**

断言页面提交 `ExportColumns`、导出表单使用统一表格 ID、项目导出默认页面格式，并保留角色授权与附件单独 Excel 行为。

- [ ] **Step 4: Run focused web tests**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~DataWorkbenchPageTests"
```

Expected: 页面授权、表单和资源契约测试通过。

### Task 4: Full verification and handoff

**Files:**
- No additional files unless verification reveals a defect.

- [ ] **Step 1: Run application and web regression tests**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj
```

- [ ] **Step 2: Build Release**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager/EngineeringManager.sln --configuration Release --no-restore
```

- [ ] **Step 3: Inspect only task-related changes**

使用 `git diff --stat`、`git diff --check` 和定向 `git status`，保留工作区既有 WIP，不进行 reset、清理或自动提交。
