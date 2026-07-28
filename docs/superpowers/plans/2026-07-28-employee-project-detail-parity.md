# Employee Project-Detail Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make employee viewing and editing follow the project-detail workspace, with a richer list dialog, automatic profile quick edit, accurate annual metric cards, business tabs, and an activity rail.

**Architecture:** Keep existing employee services and ledger calculators as the only data sources. Add view-only derived values to the employee list model, route edits to `Employees/Details?edit=profile`, and reshape the existing details Razor page around the project-detail CSS vocabulary. Employee-specific CSS remains namespaced so concurrent project-page work is preserved.

**Tech Stack:** ASP.NET Core Razor Pages, C#/.NET, vanilla JavaScript, CSS, xUnit, FluentAssertions.

---

### Task 1: Lock navigation and rich-dialog behavior with failing tests

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeIndexPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeAnnualLedgerPageTests.cs`

- [ ] Add page-contract assertions that list edit actions are links to `/Employees/Details` with `edit=profile`, while view remains a dialog action.
- [ ] Assert the view payload and dialog expose personnel, affiliation, sensitive, default-pay, annual-summary, risk, full-detail, and edit destinations.
- [ ] Assert the details page exposes a project-style header, eight metric cards, profile quick-edit panel, five horizontal tabs, and a `提示与记录` activity rail.
- [ ] Run the two test classes from an isolated repository-local Release output and confirm the new assertions fail for the missing behavior.

### Task 2: Enrich list data and redirect edit to details

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/_EmployeeEditor.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/employee-workspace.js`

- [ ] Load current-year annual summaries and penalty totals from `IEmployeeAnnualLedgerService` into read-only dictionaries keyed by employee ID.
- [ ] Serialize authorized basic fields, primary affiliation, default rates, annual summary, penalty total, overpayment flag, and full-detail/edit URLs into each row payload.
- [ ] Replace the edit dialog button with a link to `/Employees/Details?id=<id>&businessYearId=<year>&edit=profile`; keep create and copy in the existing editor flow.
- [ ] Expand both authorized and read-only dialog markup into project-style sections for basic data, affiliation, sensitive data, default pay, annual metrics, and risk.
- [ ] Update `employee-workspace.js` to populate all new fields and the full-detail/edit links without exposing values absent from the server payload.
- [ ] Rebuild and rerun Task 1 tests until green.

### Task 3: Implement automatic profile quick edit and project-style detail header

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`

- [ ] Add an optional GET-bound `Edit` property; normalize only `profile` and ignore it for users without employee-management permission.
- [ ] Render `data-inline-edit-active="true"` on the profile editor when `Edit == "profile"`, preserve it after validation or concurrency failure, and focus the first profile control from the existing inline-editor script.
- [ ] Add the smart-back action, employee identifier/name/context heading, status and copy actions using the project-detail heading hierarchy.
- [ ] Render eight annual cards from existing ledger values plus `WageEntries` penalty sum; do not subtract penalties again from the annual summary.
- [ ] Rebuild and rerun Task 1 tests until green.

### Task 4: Align profile panel, business tabs, and activity rail

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeAnnualLedgerPageTests.cs`

- [ ] Group profile fields into personnel, sensitive data, default pay, and current affiliation using project-summary cards while retaining existing form field names and handlers.
- [ ] Keep the five existing tabs and their sub-tabs, forms, attachments, source links, and URL parameters; apply the project tab-panel heading and action layout.
- [ ] Rename the right rail to `提示与记录`, render reliable ledger/wage/certificate risks before history entries, and retain independent desktop scrolling.
- [ ] Append namespaced employee-detail rules for desktop main/rail columns, metric density, profile grids, tab surfaces, and narrow-screen stacking without rewriting concurrent project selectors.
- [ ] Rebuild and rerun Task 1 tests until green.

### Task 5: Verify calculations and regressions

**Files:**
- Verify all files changed in Tasks 1–4.

- [ ] Run all tests whose fully qualified name contains `Employee`, plus inline-editing and responsive UI tests.
- [ ] Build `EngineeringManager.Web.csproj` in Release configuration to an isolated output directory and require zero warnings and zero errors.
- [ ] Run `git diff --check`, inspect all changed paths, and confirm no database migration, finance persistence, payroll contract, or project Razor file was changed by this task.
- [ ] Compare the implementation against `docs/superpowers/specs/2026-07-28-employee-project-detail-parity-design.md` and report any unavailable audit data explicitly instead of fabricating it.

Git commit, merge, push, and PR steps are intentionally omitted because project instructions require separate confirmation and the working tree contains concurrent user work.
