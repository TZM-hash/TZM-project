# Employee and Partner Workspace Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the employee-management category around the established partner-workspace layout, dialogs, actions, and responsive behavior without changing employee finance rules or persistence.

**Architecture:** Keep Razor Pages and the existing employee services as the server-side source of truth. Move list create/edit/copy handling into `Employees/Index`, add an employee-specific workspace script, and reuse the partner/equipment CSS vocabulary through isolated `employee-workspace-*` selectors. Existing deep links remain compatible through `Employees/Create`.

**Tech Stack:** ASP.NET Core Razor Pages, C# 13/.NET, vanilla JavaScript modules, CSS, xUnit, FluentAssertions.

---

### Task 1: Lock the employee workspace contract with failing tests

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeIndexPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] Add assertions that the employee index renders `employee-workspace-layout`, a sticky summary rail, an integrated data-workbench toolbar, partner-style semantic row actions, create/view/edit/copy dialogs, and `employee-workspace.js`.
- [ ] Add assertions that the page model exposes dialog state and handles create/update submissions while preserving current list filters on redirect.
- [ ] Run `dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EmployeeIndexPageTests|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore` from `EngineeringManager`; confirm the new assertions fail because the employee workspace contract is absent.

### Task 2: Implement employee list workspace and dialog data flow

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/_EmployeeEditor.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/employee-workspace.js`

- [ ] Add bound editor fields mirroring `CreateModel`, `ActiveDialog`, and POST handlers for create/update; use `EmployeeType.Temporary` as the temporary-personnel category and retain existing authorization, concurrency stamp, validation, and sensitive-data masking.
- [ ] Build the compact header, 210–220px summary rail, type quick filters, integrated toolbar, dense employee table, and semantic 查看/编辑/复制 actions using JSON payloads containing only values already authorized in the rendered DTO.
- [ ] Add create/edit/copy form and read-only detail dialogs using `mac-window-dialog`; preserve search, type, page size, and page number through a return URL.
- [ ] Implement dialog open/close, safe JSON payload hydration, copy-mode unique-field clearing, focus management, and automatic reopening after server validation errors in `employee-workspace.js`.
- [ ] Run the Task 1 test command and confirm the workspace and page-model tests pass.

### Task 3: Match partner styling without overwriting payroll changes

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] Append isolated employee workspace rules using `--app-border`, `--app-surface`, `--app-muted`, and `--app-shadow-soft`; do not rewrite or reorder the existing payroll block.
- [ ] Match partner panel radius, spacing, table density, sticky summary, semantic actions, dialog width, and 900px/680px stacking behavior.
- [ ] Run the Task 1 test command and confirm responsive and contract tests pass.

### Task 4: Align employee details, certificates, annual ledger, and sub-navigation

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/_EmployeeSubNavigation.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Ledger.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Certificates/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeIndexPageTests.cs`

- [ ] Apply the same compact heading, panel boundary, toolbar, table, status, action, and empty-state vocabulary while leaving employee-specific finance records and source links unchanged.
- [ ] Keep certificate and annual-ledger filters as URL state and preserve all current handler names and routes.
- [ ] Add markup contract assertions for the aligned subpages, then run the Task 1 test command.

### Task 5: Regression and release verification

**Files:**
- Verify all files changed in Tasks 1–4.

- [ ] Run `dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~Employee|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore`.
- [ ] Run `dotnet build .\src\EngineeringManager.Web\EngineeringManager.Web.csproj -c Release --no-restore`.
- [ ] Run `git diff --check` and inspect `git diff --name-only`; verify payroll-task files retain their existing changes and no database, shared contract, or migration file was added.
- [ ] Compare the completed implementation against every acceptance criterion in `docs/superpowers/specs/2026-07-28-employee-partner-workspace-parity-design.md` and report any remaining limitation explicitly.

Git commits are intentionally omitted because project instructions require a separate one-shot commit-plan confirmation.
