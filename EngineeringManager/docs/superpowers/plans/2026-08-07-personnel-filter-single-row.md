# 人员管理筛选栏单行布局 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 将内部人员和外部人员列表的筛选表单压缩为桌面端单行工具栏，并在窄屏下保持可用的响应式排列。

**Architecture:** 复用两个 Razor 页面已有的 `.personnel-filter-bar` 类，在 `pages.css` 中增加页面专属 Grid 规则。字段名称、查询参数、Razor PageModel 和 `IPersonnelService` 调用保持不变；Web 测试通过源码断言覆盖布局契约和筛选字段保留。

**Tech Stack:** ASP.NET Core Razor Pages, CSS Grid, xUnit, FluentAssertions, PowerShell 7 wrapper scripts.

---

### Task 1: Add the layout regression contract

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/PersonnelResponsiveUiTests.cs`

- [x] **Step 1: Add a failing CSS contract test**

Add one test that reads `wwwroot/css/pages.css` and asserts the personnel filter bar has a desktop Grid layout, compact controls, a non-wrapping action row, and `1100px`/`680px` responsive fallbacks. Also assert both personnel pages keep the `.personnel-filter-bar` class.

- [x] **Step 2: Run the focused test and confirm it fails**

Run from `EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter 'FullyQualifiedName~PersonnelResponsiveUiTests' --no-restore
```

Expected: the new CSS assertions fail because the `.personnel-filter-bar` rules are not present yet.

### Task 2: Implement the compact personnel filter layout

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [x] **Step 1: Add the desktop single-row Grid rules**

Add a `.personnel-filter-bar` block beside the existing employee workspace rules:

```css
.personnel-filter-bar {
  display: grid;
  grid-template-columns: minmax(10rem, 1.35fr) repeat(6, minmax(0, 1fr)) auto;
  align-items: end;
  gap: .55rem;
  padding: .75rem .85rem;
}
.personnel-filter-bar > label { min-width: 0; gap: .25rem; font-size: .72rem; font-weight: 650; white-space: nowrap; }
.personnel-filter-bar > label input, .personnel-filter-bar > label select { min-width: 0; min-height: 2.25rem; padding: .4rem .52rem; font-size: .78rem; }
.personnel-filter-bar > .page-actions-inline { flex-wrap: nowrap; gap: .35rem; }
.personnel-filter-bar > .page-actions-inline .button { min-height: 2.25rem; padding: .4rem .6rem; font-size: .78rem; white-space: nowrap; }
```

- [x] **Step 2: Add narrow-screen fallbacks**

At `max-width: 1100px`, use four columns, place search and actions across the available width, and let the remaining filters flow to additional rows. At `max-width: 680px`, use one column and let the two action buttons share the row.

- [x] **Step 3: Run the focused test and confirm it passes**

Run the Task 1 command again. Expected: all `PersonnelResponsiveUiTests` pass.

### Task 3: Verify unchanged personnel behavior

**Files:**
- Review only: `EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/Internal/Index.cshtml.cs`
- Review only: `EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/External/Index.cshtml.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/PersonnelPageTests.cs`

- [x] **Step 1: Run personnel page regression tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter 'FullyQualifiedName~PersonnelPageTests|FullyQualifiedName~PersonnelResponsiveUiTests|FullyQualifiedName~EmployeeAnnualLedgerPageTests|FullyQualifiedName~OrganizationDepartmentPageTests' --no-restore
```

Expected: zero failures and the recorded `PersonnelListQuery` values remain unchanged.

- [x] **Step 2: Run final verification**

Run `git diff --check`, the full test suite, Release build, and `scripts/quality-gate.ps1`. Restart the local service with `scripts/start-local-web.ps1 -Configuration Release`, then check `/health/live` and `/health/ready` return HTTP 200.
