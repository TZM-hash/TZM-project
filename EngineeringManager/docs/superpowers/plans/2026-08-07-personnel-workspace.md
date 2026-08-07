# Personnel Workspace List, Salary Columns, and Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make internal and external personnel lists use the project workbench layout, expose optional annual salary columns, and support saved column management plus filtered Excel export.

**Architecture:** Reuse the shared `DataWorkbench` for column visibility, ordering, local persistence, and toolbar behavior. Page models load the current business-year employee ledger summaries through the existing annual-ledger service, while a dedicated personnel workbook exporter maps a server-validated row projection to Excel. No database schema changes are required.

**Tech Stack:** ASP.NET Core Razor Pages, application service interfaces, existing `EmployeeAnnualLedgerSummary`, `SimpleXlsxWorkbook`, shared DataWorkbench JavaScript/CSS, xUnit/FluentAssertions.

---

### Task 1: Add failing contracts and page assertions

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/PersonnelPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/PersonnelResponsiveUiTests.cs`
- Create: `tests/EngineeringManager.Tests/Application/PersonnelWorkbookExportTests.cs`

- [ ] Add assertions that both personnel pages contain `_DataWorkbench`, data-column keys for the personnel and salary fields, selection checkboxes, and the personnel export handler.
- [ ] Add assertions for fixed-width table rules, controlled overflow, compact personnel-number styling, and a no-wrap action row.
- [ ] Add a workbook test that requests a selected set of columns and verifies the worksheet headers and values, including salary columns and blank salary values for a non-employee.
- [ ] Run the focused tests and confirm they fail because the workbench, export contract, and salary projection do not yet exist.

### Task 2: Define the salary projection and workbook contract

**Files:**
- Create: `src/EngineeringManager.Application/DataExchange/IPersonnelWorkbookService.cs`
- Create: `src/EngineeringManager.Application/DataExchange/PersonnelWorkbookDtos.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/PersonnelWorkbookExporter.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`

- [ ] Define the server-side personnel export column catalog and a row projection containing the public personnel fields plus optional annual salary summary values.
- [ ] Include the annual ledger fields: carry-forward, wage payable, expense payable, other payable, adjustment, current-year payable, received, current-year unpaid, current balance, settlement progress, overpaid status, and penalty deduction.
- [ ] Implement a whitelist-based exporter using `SimpleXlsxWorkbook`, preserving full personnel numbers and leaving salary cells blank when no employee ledger exists.
- [ ] Register the exporter in dependency injection.
- [ ] Run the workbook tests and make them pass.

### Task 3: Load annual summaries and implement internal/external page state

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Personnel/Internal/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Personnel/External/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`

- [ ] Inject the existing business-year and employee annual-ledger services as optional dependencies so current page tests remain constructible.
- [ ] Resolve the current business year using the same date-based fallback as the employee page, load summaries for linked employees, calculate penalty deductions from existing wage entries, and keep non-employee personnel without a summary.
- [ ] Add page-specific workbench definitions, saved-view definitions, export state, selected-person IDs, filter preservation, and server-side intersection of selected IDs with the current personnel query.
- [ ] Add the `SaveView` and `Export` handlers, normalize requested column keys, and save independent export-column choices using `ISavedDataViewService`.
- [ ] Run page-model tests and correct constructor, authorization, and filter-preservation failures.

### Task 4: Implement the shared personnel table and project-style export menu

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Personnel/Internal/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Personnel/External/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Personnel/_PersonnelWorkbookExport.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] Replace grouped static cells with data-column-key cells and add selection controls plus the workbench toolbar to both scopes.
- [ ] Render compact personnel numbers with full-value titles, separate organization/project/crew fields, optional salary columns, and blank salary values for external records without employee ledgers.
- [ ] Render project-style export scope and column-source controls, including current workbench columns and independent saved export columns.
- [ ] Add fixed table widths, internal horizontal scrolling, ellipsis/wrapping rules, numeric salary alignment, and a single-line action flex row for desktop and responsive fallbacks.
- [ ] Run the focused page and asset tests.

### Task 5: Verify all behavior

**Files:**
- No source changes expected unless verification finds a defect.

- [ ] Run the full test suite from `EngineeringManager`.
- [ ] Run `dotnet build EngineeringManager.sln --no-restore` (or the repository-approved equivalent) and confirm a clean build.
- [ ] Confirm the running service responds with HTTP 200 on `/health/live` and `/health/ready`.
- [ ] Review the diff and confirm no prior user changes or test-data cleanup files were reverted.
