# Construction Crew Workspace Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the construction crew index as a partner-style management workspace while keeping roster and payroll history on the existing details page.

**Architecture:** Merge existing construction crew metrics with full business-partner management DTOs in the Razor Page model. Keep all new UI behavior page-scoped through a dedicated script and CSS classes, and deep-link roster and finance actions to stable anchors on the existing details page.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, Entity Framework-backed application services, native JavaScript, existing DataWorkbench/CSS system, xUnit, FluentAssertions.

**Execution constraints:** Work inline in the current workspace; do not create a worktree or subagent. Do not stage, commit, or push without separate user confirmation. Preserve unrelated working-tree changes.

---

## File Map

- Modify `EngineeringManager/tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`: lock the workspace, filters, dialogs, actions, anchors, script, and styles into a source contract.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`: load merged workspace rows, filter summaries, permissions, and create/update handling.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml`: render the partner-style workspace and Mac dialogs.
- Create `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/crew-workspace.js`: filter auto-submit and dialog behavior.
- Modify `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`: scoped crew workspace, table, dialog, and responsive layout.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`: align crew columns with the rebuilt table.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml`: add stable roster, finance, and payroll anchors only.

### Task 1: Lock The Crew Workspace Contract

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`

- [ ] **Step 1: Add a failing workspace contract test**

Require the crew index and supporting assets to contain the approved structure:

```csharp
[Fact]
public void CrewIndexUsesPartnerStyleWorkspaceDialogsAndDeepLinks()
{
    var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
    var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml.cs");
    var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Details.cshtml");
    var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "crew-workspace.js");
    var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

    index.Should().Contain("data-crew-workspace")
        .And.Contain("crew-workspace-layout")
        .And.Contain("data-crew-dialog-open=\"create\"")
        .And.Contain("data-crew-dialog-open=\"details\"")
        .And.Contain("data-crew-dialog-open=\"edit\"")
        .And.Contain("data-crew-dialog-open=\"copy\"")
        .And.Contain("#crew-roster")
        .And.Contain("#crew-finance");
    model.Should().Contain("IBusinessPartnerService partnerService")
        .And.Contain("OnPostSaveAsync")
        .And.Contain("BusinessPartnerRoleType.ConstructionCrew");
    details.Should().Contain("id=\"crew-roster\"")
        .And.Contain("id=\"crew-finance\"")
        .And.Contain("id=\"crew-payroll\"");
    script.Should().Contain("[data-crew-workspace]")
        .And.Contain("mode === \"copy\"");
    css.Should().Contain(".crew-workspace-layout")
        .And.Contain(".crew-workspace-table");
}
```

- [ ] **Step 2: Run the focused test and confirm RED**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter FullyQualifiedName~ConstructionCrewPageTests --no-restore
```

Expected: FAIL because the crew workspace, dialogs, script, and anchors do not exist.

### Task 2: Build The Merged Page Model And Save Path

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`

- [ ] **Step 1: Add workspace properties, filters, and permissions**

Use existing services and a page-local row model:

```csharp
public IReadOnlyList<CrewWorkspaceRow> AllCrews { get; private set; } = [];
public IReadOnlyList<CrewWorkspaceRow> Crews { get; private set; } = [];
public IReadOnlyList<CrewTradeSummary> TradeSummaries { get; private set; } = [];
public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator)
    || User.IsInRole(SystemRoles.ApplicationAdministrator)
    || User.IsInRole(SystemRoles.ProjectManager);
public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator)
    || User.IsInRole(SystemRoles.ApplicationAdministrator)
    || User.IsInRole(SystemRoles.Finance);
[BindProperty(SupportsGet = true)] public string? Trade { get; set; }
[BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
[BindProperty] public CrewEditorInput Editor { get; set; } = new();
```

- [ ] **Step 2: Merge crew metrics with partner management DTOs**

Load all crews with `includeInactive: true`, load full construction-crew partners through `ListForManagementAsync`, join by ID, calculate trade summaries from the unfiltered set, then apply search, trade, and status filters.

```csharp
var metrics = await crewService.ListAsync(true, Search, CanViewSensitive, cancellationToken);
var partnerMap = (await partnerService.ListForManagementAsync(null, BusinessPartnerRoleType.ConstructionCrew, cancellationToken))
    .ToDictionary(item => item.Id);
var rows = metrics.Where(item => partnerMap.ContainsKey(item.Id))
    .Select(item => new CrewWorkspaceRow(partnerMap[item.Id], item))
    .ToArray();
Crews = rows
    .Where(item => string.IsNullOrWhiteSpace(Trade) || item.TradeCategory == Trade)
    .Where(item => !IsActive.HasValue || item.Metrics.IsActive == IsActive.Value)
    .ToArray();
```

- [ ] **Step 3: Add create and update handling**

Implement `OnPostSaveAsync` using `IBusinessPartnerService`. Always construct `PartnerRoleRequest` with `BusinessPartnerRoleType.ConstructionCrew`; create uses `CreateBusinessPartnerRequest`, edit uses `UpdateBusinessPartnerRequest` with concurrency stamp, status, and reason. Catch `ArgumentException`, `InvalidOperationException`, and `DbUpdateConcurrencyException`, reload the page, and reopen the editor.

- [ ] **Step 4: Build to catch model and Razor contract errors**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 build .\EngineeringManager.sln -c Debug --no-restore
```

Expected: PASS after the page markup is completed in Task 3; compile errors encountered before that point must be limited to temporarily missing markup references.

### Task 3: Render The Workspace And Dialog Interactions

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/crew-workspace.js`

- [ ] **Step 1: Replace the index markup with the approved workspace**

Render:

```html
<div class="crew-workspace-page" data-crew-workspace>
  <section class="page-heading compact-page-heading crew-workspace-heading">...</section>
  <section class="crew-workspace-layout">
    <aside class="crew-workspace-summary">...</aside>
    <section class="crew-workspace-list">
      <div class="equipment-list-toolbar equipment-list-toolbar--integrated crew-list-toolbar">...</div>
      <div class="table-wrap crew-table-wrap">
        <table class="data-table crew-workspace-table" id="crews-table">...</table>
      </div>
    </section>
  </section>
</div>
```

The table must render crew identity, trade/contact, workers, projects, total paid, last payment, status, and actions. Use JSON payloads for the read-only and editor dialogs. Render finance only when `CanManageFinance` and editor actions only when `CanManage`.

- [ ] **Step 2: Add Mac-style details and editor dialogs**

Use existing `mac-window-dialog`, `workbench-dialog-heading`, `quick-edit-actions`, and `equipment-detail-grid` components. The editor must post `Editor.*` fields and keep Search, Trade, and IsActive hidden values.

- [ ] **Step 3: Implement page-scoped JavaScript**

```javascript
const page = document.querySelector("[data-crew-workspace]");
if (page) {
  const editorDialog = page.querySelector("[data-crew-editor-dialog]");
  const detailsDialog = page.querySelector("[data-crew-details-dialog]");
  const show = (dialog) => {
    if (dialog && !dialog.open) dialog.showModal();
  };
  // Auto-submit select filters, populate details, and initialize create/edit/copy fields.
}
```

Copy mode must append `-COPY` to the number, append a copy suffix to names, clear credit code and contact data, force active status, and retain the construction-crew role through the server model.

- [ ] **Step 4: Run the crew page test**

Run the Task 1 command. Expected: remaining failures point only to styles, preset columns, or details anchors handled in Task 4.

### Task 4: Add Scoped Styles, Columns, And Details Anchors

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml`

- [ ] **Step 1: Add crew workspace styles**

Implement a 220px summary column, fixed-layout table widths, non-wrapping action row, two-line crew names, tabular numeric amounts, Mac dialog grids, and 900px/680px responsive rules. Keep selectors under `crew-workspace-*`, `crew-table-*`, or `crew-*` page classes.

- [ ] **Step 2: Align DataWorkbench columns**

Keep the preset keys exactly synchronized with the rendered table:

```csharp
public static DataWorkbenchViewModel Crews => Create("crews", "crews-table", [
    ("crew", "班组"), ("trade_contact", "专业 / 负责人"), ("workers", "当前人员"),
    ("projects", "参与项目"), ("paid", "累计代发工程款"),
    ("last_payment", "最近发放"), ("status", "状态"), ("actions", "操作")]);
```

- [ ] **Step 3: Add stable details anchors**

Set `id="crew-finance"` on the finance section, `id="crew-roster"` on the roster section, and `id="crew-payroll"` on the payroll history section. Do not otherwise restructure the details page.

- [ ] **Step 4: Run focused tests and build**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~ModuleDataWorkbenchTests|FullyQualifiedName~ResponsiveUiAssetTests' --no-restore
```

Expected: all selected tests pass.

### Task 5: Regression And Browser Verification

**Files:**
- Verify only; do not modify unrelated files.

- [ ] **Step 1: Run focused crew and partner tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~PartnerWorkspacePageTests|FullyQualifiedName~ModuleDataWorkbenchTests|FullyQualifiedName~ResponsiveUiAssetTests' --no-restore
```

- [ ] **Step 2: Run a Release build and diff check**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 build .\EngineeringManager.sln -c Release --no-restore
git diff --check
```

Expected: zero failed tests, zero build warnings/errors, and no whitespace errors.

- [ ] **Step 3: Verify in the browser**

At screenshot-equivalent desktop width, 1366px, and 390px verify: workspace alignment, summary counts, trade/status filters, table column visibility, create/details/edit/copy dialogs, personnel/finance deep links, no clipped button text, table-only horizontal scrolling, and no console errors.
