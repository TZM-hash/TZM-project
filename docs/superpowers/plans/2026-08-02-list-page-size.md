# 全局列表分页条数 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为每个可见数据列表提供独立的 20/50/100 条每页选择，并让服务端分页页与客户端完整列表保持一致的筛选、排序和导航体验。

**Architecture:** 新增共享 `list-pagination.js`，识别工作台列表、独立客户端表格和已有服务端分页表格。工作台客户端列表由脚本切分 DOM 行；项目、员工、财务兼容页和中央账本主表继续使用服务端 PageSize，并由脚本为无工作台的中央账本主表补充选择器/导航。所有选择器使用独立表格 key，动态行和空状态安全处理。

**Tech Stack:** ASP.NET Core Razor Pages, C# page models, ES modules, existing `data-table.js`/`site.js`, xUnit + FluentAssertions, .NET 8.

---

### Task 1: 建立分页契约测试（RED）

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/ListPaginationAssetTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`

- [ ] **Step 1: Write the failing tests**

在 `ListPaginationAssetTests` 中加入以下契约：

```csharp
[Fact]
public void SharedPaginationAssetSupportsIndependentTablesAndPageSizes()
{
    var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "list-pagination.js");

    script.Should().Contain("table.data-table:not(.sr-only):not([data-list-pagination-disabled])")
        .And.Contain("20, 50, 100")
        .And.Contain("engineering-manager-list-pagination")
        .And.Contain("data-current-page-size")
        .And.Contain("data-list-pagination-server")
        .And.Contain("MutationObserver")
        .And.Contain("aria-label", "分页控件必须可访问");
}

[Fact]
public void SiteLoadsPaginationForWorkbenchAndStandaloneTables()
{
    var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

    site.Should().Contain("./components/list-pagination.js")
        .And.Contain("initListPagination");
}

[Fact]
public void EverySharedPresetAllowsPageSizeSelection()
{
    var presets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");

    presets.Should().Contain("CanChangePageSize: true");
    presets.Should().NotContain("CanChangePageSize: false");
}

[Theory]
[InlineData("Ledger/External/Index.cshtml")]
[InlineData("Ledger/Internal/Index.cshtml")]
public void CentralLedgerMainTablesExposeServerPaginationState(string relativePath)
{
    var razor = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", relativePath.Replace('/', Path.DirectorySeparatorChar)));

    razor.Should().Contain("data-list-pagination-server=\"true\"")
        .And.Contain("data-list-pagination-current-page")
        .And.Contain("data-list-pagination-total-pages")
        .And.Contain("data-list-pagination-page-size");
}
```

同时在 `DataWorkbenchPageTests` 增加：

```csharp
[Fact]
public void ExistingServerPaginatedPagesExposeTheUnifiedPageSizeOptions()
{
    var partial = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml");
    partial.Should().Contain("每页显示条数").And.Contain("20").And.Contain("50").And.Contain("100");

    foreach (var page in new[] { "Projects", "Employees", "Finance" })
    {
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", page, "Index.cshtml");
        razor.Should().Contain("_DataWorkbench");
    }
}
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ListPaginationAssetTests|FullyQualifiedName~DataWorkbenchPageTests"
```

Expected: FAIL because `list-pagination.js` is absent, presets explicitly disable page-size selection, and central ledger tables do not expose pagination metadata.

### Task 2: Implement the shared client/server pagination module

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/list-pagination.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js:23-36`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/data-table.js:1,186-194`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml:25-31`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`

- [ ] **Step 1: Add the page-size constants, state key and validation**

Implement `PAGE_SIZES = [20, 50, 100]`, `normalizePageSize`, `normalizePage`, and a per-table storage key built from the workbench page key or `window.location.pathname` plus table id/index.

- [ ] **Step 2: Add client-side row slicing**

Implement `businessRows`, fixed-row detection, `renderClientPage`, and a `MutationObserver`. Fixed summary/empty rows remain visible; ordinary rows are hidden outside the active range. A table’s state is kept in a `WeakMap`, so two tables on one page cannot share page numbers.

- [ ] **Step 3: Add standalone controls and server navigation**

Generate a `.standalone-list-pagination` bar for tables outside a workbench. Use `label.page-size-picker` and `nav.table-pagination` with `aria-label="分页控件"`. Server mode uses `data-list-pagination-current-page`, `data-list-pagination-total-pages`, and `data-list-pagination-total-count` to build URL links while preserving all unrelated query parameters; client mode keeps the selection in local storage and does not reload the page.

- [ ] **Step 4: Wire workbench selectors without double binding**

Add `data-list-pagination-server` to `_DataWorkbench`. Change `data-table.js` so its existing page-size listener only navigates for server workbenches; client workbenches dispatch to the shared paginator. Load and initialize `list-pagination.js` beside the existing table, saved-view and filter modules in `site.js`.

- [ ] **Step 5: Add shared styles**

Add only token-based rules for `.standalone-list-pagination`, its label, and its navigation; reuse `.page-size-picker`, `.table-pagination`, `--app-border`, `--app-surface`, and existing mobile flex wrapping.

- [ ] **Step 6: Run focused asset tests**

Run the Task 1 filter again. Expected: JavaScript and Razor contracts pass, while server metadata tests remain red until Task 3.

### Task 3: Enable every list and expose server pagination metadata

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs:24-41`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs:100-120`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs:106-110`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Finance/Index.cshtml.cs:71-84`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml:81`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml:71`

- [ ] **Step 1: Enable the shared selector on DataWorkbench presets**

Change the preset factory’s `CanChangePageSize` value to `true`. Keep Projects/Finance/Employees’ existing server `PageSize` binding and sorting state.

- [ ] **Step 2: Normalize page-size state before building server workbenches**

In Projects, Finance, Employees, External Ledger and Internal Ledger load paths, normalize `PageSize` to 20 unless it is 20, 50 or 100 before rendering the workbench/metadata. Preserve existing page-number clamping and service-side normalization.

- [ ] **Step 3: Mark central ledger settlement tables as server paginated**

Add `data-list-pagination-server="true"`, `data-list-pagination-current-page`, `data-list-pagination-total-pages`, `data-list-pagination-total-count`, and `data-list-pagination-page-size` to the settlement table in both ledger pages. The generic module will generate the independent selector and navigation for this table; auxiliary invoice/cash/payroll tables remain client-paginated.

- [ ] **Step 4: Run focused tests to verify GREEN**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ListPaginationAssetTests|FullyQualifiedName~DataWorkbenchPageTests|FullyQualifiedName~EmployeeIndexPageTests|FullyQualifiedName~CentralLedgerPageTests"
```

Expected: PASS.

### Task 4: Add model/query regression coverage

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeIndexPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeListSortingTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`

- [ ] **Step 1: Add valid page-size cases**

Assert that 20, 50 and 100 are accepted by each server page model/query and that the returned row count never exceeds the selected size.

- [ ] **Step 2: Add invalid page-size fallback cases**

Assert that 0, 10 and 999 normalize to 20 at the page boundary or query service boundary, without changing active search/sort parameters.

- [ ] **Step 3: Run model regression tests**

Run the three test files’ filters and expected result is PASS.

### Task 5: Full verification and handoff

**Files:**
- No additional source files unless verification reveals a defect.

- [ ] **Step 1: Run all Web and Application tests**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/EngineeringManager.sln
```

Expected: all tests pass with zero failures.

- [ ] **Step 2: Build Release**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager/EngineeringManager.sln --configuration Release --no-restore
```

Expected: zero errors and zero warnings.

- [ ] **Step 3: Inspect the final diff**

Use `git status --short` and `git diff --stat`; keep unrelated pre-existing sorting/ledger files out of any commit plan unless they are explicitly part of this feature.

- [ ] **Step 4: Present the batched commit plan**

Follow the project AGENTS.md commit gate: list only files changed for this feature, list unrecognized dirty files separately, and wait for the user’s one-shot confirmation before staging/committing. Do not push without a separate explicit request.
