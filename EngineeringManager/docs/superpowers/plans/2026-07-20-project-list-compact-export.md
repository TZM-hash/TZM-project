# Project List Compact Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the project page's overview export and full-width workbook panel with a compact toolbar workbook-export dropdown.

**Architecture:** Keep the existing project workbook handler and checkbox/form contract. Add a project-specific action slot to the shared workbench view model so the Projects page can render its compact export form inside the shared toolbar without affecting Finance or other consumers.

**Tech Stack:** ASP.NET Core Razor Pages, C#, CSS, vanilla JavaScript, xUnit, FluentAssertions.

---

### Task 1: Lock the desired markup contract

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`

- [ ] Add assertions that Projects renders the compact toolbar export hook and no longer renders the old full-width form or “导出当前视图”.
- [ ] Add assertions that the legacy project overview `Export` handler and hidden export form are removed.
- [ ] Run the focused tests and verify they fail for the missing compact toolbar contract.

### Task 2: Move workbook export into the toolbar

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Shared/DataWorkbenchViewModel.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`

- [ ] Add an optional project-toolbar export partial contract to `DataWorkbenchViewModel`.
- [ ] Render the compact `details` form in the shared toolbar only when Projects supplies it.
- [ ] Remove the old workbook panel, hidden overview export form, `OnPostExportAsync`, `SelectedFields`, and `IExportService` dependency.
- [ ] Keep the workbook form id so row checkboxes remain associated with the form.
- [ ] Run the focused tests and verify they pass.

### Task 3: Style and interaction polish

**Files:**
- Modify: `src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `src/EngineeringManager.Web/wwwroot/js/components/check-selector.js`

- [ ] Add right-aligned, viewport-safe menu styling and compact section hierarchy.
- [ ] Preserve selected-count updates, mutual exclusion, attachment synchronization, Escape close, and outside-click close.
- [ ] Run all tests and build the solution.

### Task 4: Rendered QA

**Files:**
- No committed QA artifacts.

- [ ] Start the application at `http://localhost:5075`.
- [ ] Verify Projects page identity, non-blank rendering, console health, and menu interaction at desktop size.
- [ ] Verify the menu stays inside the viewport at mobile size.
- [ ] Capture screenshots outside the repository and inspect them with `view_image` against the supplied reference screenshot.
