# Project Management Workbook Data Exchange Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans and superpowers:test-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not create commits, branches, pushes, migrations, or production database changes unless separately authorized.

**Goal:** Build a project-management workbook that exports filtered or manually selected projects into selectable detail sheets and safely imports standard, mapped, or attachment ZIP workbooks as one atomic batch.

**Architecture:** Keep the existing `IExportService` and `IImportService` behavior for other modules. Add a project-specific application contract backed by a reusable workbook catalog, a project exporter, a project importer, and an attachment archive helper; the public service coordinates these components and records existing data-exchange tasks/batches without introducing a database migration.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQLite/SQL Server, C#, existing ZIP + Open XML XLSX utilities, xUnit, FluentAssertions, PowerShell 7.

**Source specification:** `docs/superpowers/specs/2026-07-19-project-management-workbook-data-exchange-design.md`

**Execution policy:** Plan creation uses the user's requested highest reasoning intensity. Implementation uses high reasoning intensity. Work remains serial because the catalog, XLSX metadata, exporter, importer, and page contracts share evolving types; dispatch a subagent only if a later test-only or UI-only task becomes independent after those contracts stabilize.

---

## File Structure

### Create

- `src/EngineeringManager.Application/DataExchange/IProjectWorkbookService.cs` - public project workbook export/import contract.
- `src/EngineeringManager.Application/DataExchange/ProjectWorkbookDtos.cs` - sheet enum, catalog DTOs, filter/scope requests, preview DTOs, and workbook version constants.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookCatalog.cs` - explicit sheet and field definitions, importability, hidden columns, aliases, and dependency order.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs` - selected-project queries and row materialization for all project sheets.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs` - standard/mapped workbook parsing, validation, dependency resolution, and atomic writes.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookArchive.cs` - project-scoped ZIP creation/parsing, manifest/checksum validation, and attachment staging/compensation.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs` - orchestration, authorization-scope resolution, task/batch persistence, preview, and confirm.
- `tests/EngineeringManager.Tests/Application/ProjectWorkbookCatalogTests.cs` - catalog completeness and dependency tests.
- `tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs` - selection, sheet, row, summary, and ZIP export tests.
- `tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs` - cross-sheet create/update, mapping, blank, concurrency, atomicity, and attachment import tests.

### Modify

- `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxWorkbook.cs` - hidden/protected technical-column support and workbook metadata sheet state.
- `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxReader.cs` - preserve blank columns and expose sheet metadata needed by project imports.
- `src/EngineeringManager.Application/Projects/ProjectDtos.cs` - add an optional `IncludeInactive` filter flag while preserving existing defaults.
- `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs` - honor `IncludeInactive` in authorized filtering.
- `src/EngineeringManager.Web/Program.cs` - register project workbook components.
- `src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs` - filtered/manual project workbook export handler and bound selections.
- `src/EngineeringManager.Web/Pages/Projects/Index.cshtml` - row selection and workbook sheet selector.
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs` - current-project workbook export handler.
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml` - project workbook export action.
- `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml.cs` - project workbook export/import/confirm handlers.
- `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml` - project-specific workbook controls and per-sheet preview.
- `src/EngineeringManager.Web/Pages/DataExchange/DataExchangeLabels.cs` - project workbook sheet labels.
- `src/EngineeringManager.Web/wwwroot/js/components/check-selector.js` - selection count and cross-page “all matching” mode without losing manual IDs.
- `src/EngineeringManager.Web/wwwroot/css/components.css` - compact project/workbook selection controls.
- `tests/EngineeringManager.Tests/Infrastructure/SimpleXlsxWorkbookTests.cs` - hidden columns, sheet protection, and blank-column round trip.
- `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs` - inactive-project filter behavior.
- `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs` - list/detail export authorization and handlers.
- `tests/EngineeringManager.Tests/Web/DataExchangeBackupAuthorizationTests.cs` - admin-only import and project workbook controls.

### Preserve

- Existing `ExportService`/`ImportService` behavior and tests for employees, companies, equipment, payroll, and legacy single-data-set flows.
- Existing project current-view export handler semantics; the workbook export uses a separate handler.
- Existing uncommitted user changes in all touched files.

---

### Task 1: Define the project workbook contract and explicit catalog

**Files:**
- Create: `src/EngineeringManager.Application/DataExchange/IProjectWorkbookService.cs`
- Create: `src/EngineeringManager.Application/DataExchange/ProjectWorkbookDtos.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookCatalog.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

Add tests asserting the exact ordered sheet sequence:

```csharp
ProjectWorkbookCatalog.Sheets.Select(item => item.Sheet).Should().Equal(
    ProjectWorkbookSheet.ProjectMaster,
    ProjectWorkbookSheet.ProjectSummary,
    ProjectWorkbookSheet.Contracts,
    ProjectWorkbookSheet.QuantityLines,
    ProjectWorkbookSheet.Milestones,
    ProjectWorkbookSheet.Assignments,
    ProjectWorkbookSheet.Partners,
    ProjectWorkbookSheet.Construction,
    ProjectWorkbookSheet.StageResults,
    ProjectWorkbookSheet.Receivables,
    ProjectWorkbookSheet.Collections,
    ProjectWorkbookSheet.Payables,
    ProjectWorkbookSheet.Payments,
    ProjectWorkbookSheet.Invoices,
    ProjectWorkbookSheet.Attachments);
```

Assert that `ProjectSummary` is export-only, `Attachments` requires ZIP, every importable sheet has `project_number`, `_system_id`, `_project_system_id`, `_concurrency_stamp`, and `_dataset_version` as applicable, and every non-root sheet has a dependency preceding it.

- [ ] **Step 2: Run the catalog tests and verify RED**

Run:

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectWorkbookCatalogTests
```

Expected: compile failure because the project workbook types do not exist.

- [ ] **Step 3: Add the application contract**

Define `ProjectWorkbookSheet`, `ProjectWorkbookFieldDto`, `ProjectWorkbookSheetDto`, `ProjectWorkbookScope`, `ProjectWorkbookExportRequest`, `ProjectWorkbookImportRequest`, `ProjectWorkbookSheetPreviewDto`, and `ProjectWorkbookImportPreviewDto`. Use version constants:

```csharp
public static class ProjectWorkbookVersions
{
    public const string Workbook = "project-workbook/1";
    public const string Dataset = "1";
}
```

`ProjectWorkbookScope` carries `ProjectListActor`, `ProjectListQuery`, `SelectAllMatching`, and explicit `SelectedProjectIds`. `IProjectWorkbookService` exposes `GetSheets()`, `ExportAsync`, `PreviewAsync`, and `ConfirmAsync`.

- [ ] **Step 4: Implement the explicit catalog**

Create immutable definitions for the 15 business sheets with stable sheet names, field keys, Chinese headers, types, required/importable/calculated/hidden flags, aliases, and dependencies. Keep `目录/说明` as workbook metadata generated by the exporter, not a selectable business sheet.

- [ ] **Step 5: Run the catalog tests and verify GREEN**

Run the Task 1 command. Expected: all `ProjectWorkbookCatalogTests` pass.

---

### Task 2: Extend XLSX support for protected technical columns

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxWorkbook.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxReader.cs`
- Test: `tests/EngineeringManager.Tests/Infrastructure/SimpleXlsxWorkbookTests.cs`

- [ ] **Step 1: Write failing XLSX metadata tests**

Add tests that create a sheet with visible business columns and hidden technical columns, then inspect `xl/worksheets/sheet1.xml` for `<cols><col hidden="1" ... /></cols>` and `<sheetProtection ... />`. Add a round-trip case whose middle cell is blank and whose later technical columns remain at their original indexes.

- [ ] **Step 2: Run the XLSX tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~SimpleXlsxWorkbookTests
```

Expected: new hidden/protection assertions fail.

- [ ] **Step 3: Add worksheet options**

Add `XlsxWorksheetOptions` with `HiddenColumnIndexes`, `ProtectSheet`, and `HiddenSheet`. Preserve the current `AddWorksheet` overload by forwarding to a new overload so existing callers remain source-compatible.

- [ ] **Step 4: Emit Open XML column and protection metadata**

Write `<cols>` before `<sheetData>`, coalesce adjacent hidden indexes into ranges, emit sheet protection after `sheetData`, and mark the generated metadata sheet `state="veryHidden"` in `workbook.xml`.

- [ ] **Step 5: Preserve sparse columns in the reader**

Use cell references to insert all missing cells before each value and pad each row to the header column count. Return sheet visibility metadata without breaking current `SimpleXlsxSheet.Name/Rows` consumers.

- [ ] **Step 6: Run XLSX tests and verify GREEN**

Run the Task 2 command. Expected: existing hyperlink/typed-cell tests and new metadata tests all pass.

---

### Task 3: Reuse authorized project filtering, including inactive projects

**Files:**
- Modify: `src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`

- [ ] **Step 1: Write failing inactive-scope tests**

Seed one active and one inactive project assigned to the same actor. Assert the existing query returns only active data and `query with { IncludeInactive = true }` returns both while still excluding projects outside the actor's assignment scope.

- [ ] **Step 2: Run the project service test and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectServiceTests
```

Expected: compile failure for `IncludeInactive`.

- [ ] **Step 3: Add the backward-compatible filter flag**

Append `bool IncludeInactive = false` to `ProjectListQuery`. Change the base query predicate to `query.IncludeInactive || project.IsActive`; retain all current keyword, stage, company, responsible-user, amount, affiliation, authorization, sort, pagination, and matching-ID behavior.

- [ ] **Step 4: Run project service tests and verify GREEN**

Run the Task 3 command. Expected: new and existing project query tests pass.

---

### Task 4: Export selected project sheets and read-only summaries

**Files:**
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs`

- [ ] **Step 1: Write failing filtered/manual export tests**

Seed two authorized projects and one unauthorized project with contracts, line items, milestones, assignments, partners, construction records, stage results, receivables, collections, payables, payments, and invoices. Assert:

- `SelectAllMatching` exports all cross-page matching project IDs.
- explicit IDs intersect the matching authorized IDs.
- selected sheets yield `目录/说明` plus exactly those sheets.
- all-sheet export contains the read-only summary.
- inactive projects appear only with `IncludeInactive = true`.

- [ ] **Step 2: Run export tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectWorkbookExportTests
```

Expected: compile failure because exporter/service do not exist.

- [ ] **Step 3: Implement project scope resolution**

Use `IProjectService.SearchProjectsAsync` with the supplied actor/query to obtain authorized matching IDs. For manual mode, intersect them with explicit IDs. Reject an empty final set with `InvalidOperationException("没有可导出的项目。")`.

- [ ] **Step 4: Implement metadata and project master/summary rows**

Generate `目录/说明` with workbook version, generated time, selected project count, filter summary, sheet key/name/importability, and dependency. Generate `项目主档` with all editable project fields and protected technical columns. Generate `项目经营汇总` from `ProjectSummaryService` and `IFinanceLedgerService`, never exposing it as importable.

- [ ] **Step 5: Implement all detail row producers**

Materialize rows for contracts, quantity lines, milestones, assignments, project partners, construction, stage results, receivables, collections, payables, payments, and invoices. Every row carries readable business keys plus stable IDs/concurrency stamps. Query only final project IDs and order by project number plus the sheet's stable business key/date.

- [ ] **Step 6: Record the export task**

Persist one `DataExchangeTask` with direction export, dataset marker `ProjectOverview`, serialized project workbook sheet keys, filter/scope JSON, row count, result content, content type, file name, SHA-256, and completed status. Do not add a migration.

- [ ] **Step 7: Register the service and run export tests**

Register `IProjectWorkbookService` with its concrete service and dependencies. Run the Task 4 command. Expected: all project workbook export tests pass.

---

### Task 5: Add project-scoped attachment ZIP export

**Files:**
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookArchive.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookExportTests.cs`

- [ ] **Step 1: Write failing attachment export tests**

Use a temporary `LocalFileStore` and seed project-, contract-, and stage-result-linked attachments for selected and unselected projects. Assert the ZIP contains only selected-project files, `project-workbook.xlsx`, `manifest.json`, `checksums.sha256`, and an `附件清单` sheet with relation type, relative path, size, and SHA-256.

- [ ] **Step 2: Run the attachment export test and verify RED**

Run the Task 4 command. Expected: attachment ZIP assertions fail.

- [ ] **Step 3: Implement safe ZIP generation**

Filter attachments by final project IDs through `ProjectId`, `Contract.ProjectId`, or `StageResult.ProjectId`. Sanitize leaf names, store files under `attachments/{attachmentId:N}/{safeName}`, stream through SHA-256, and write manifest/checksum entries. Fail when attachments are requested without an `IFileStore`.

- [ ] **Step 4: Run export tests and verify GREEN**

Run the Task 4 command. Expected: workbook and attachment ZIP tests pass.

---

### Task 6: Preview and atomically import standard project workbooks

**Files:**
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`

- [ ] **Step 1: Write failing standard import tests**

Cover these independent behaviors:

- a complete workbook creates a project, contract, quantity line, receivable, collection, payable, payment, and invoice in dependency order;
- a single quantity sheet requires an existing project and contract;
- hidden system IDs update existing rows and rotate concurrency stamps;
- stale concurrency produces a sheet/row/column error and blocks confirm;
- a missing column preserves the field, while a present blank clears nullable fields;
- one invalid row in any sheet keeps every table unchanged after confirm is attempted;
- missing rows and missing sheets never delete records.

- [ ] **Step 2: Run import tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectWorkbookImportTests
```

Expected: compile failure because importer behavior is absent.

- [ ] **Step 3: Parse and normalize standard sheets**

Identify the workbook by its very-hidden metadata sheet and version. Map headers using the explicit catalog, preserve whether each source column exists, normalize cell values with invariant decimal/date/boolean parsing, and retain original sheet/row/column coordinates for errors.

- [ ] **Step 4: Validate the entire workbook without writes**

Validate required fields, types, enums, duplicate system/business keys, project/parent links, import mode, concurrency stamps, read-only sheets, and all cross-sheet dependencies. Build per-sheet totals for new/update/unchanged/skipped/error. Persist the original bytes and mapping/options in one existing `ImportBatch` using the `ProjectOverview` marker.

- [ ] **Step 5: Implement dependency-ordered atomic writes**

Open one EF transaction. Resolve existing entities by `_system_id` first and business keys second. Create/update project master, project associations, contracts, quantity lines, stage results, finance rows, and other details in catalog dependency order. Rotate concurrency stamps, use existing validation rules/services where they preserve the outer transaction, and roll back on any exception.

- [ ] **Step 6: Prevent implicit deletion and recompute summaries**

Never remove entities because they are absent. Apply explicit active/void/status values only. Let existing project summary and finance queries recompute from committed detail data; never import summary rows.

- [ ] **Step 7: Run import tests and verify GREEN**

Run the Task 6 command. Expected: standard, update, blank, concurrency, dependency, and atomicity tests pass.

---

### Task 7: Support arbitrary Excel mapping and attachment ZIP import

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookArchive.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`

- [ ] **Step 1: Write failing mapped import tests**

Create a nonstandard sheet with headers such as `工程编号`, `工程名称`, and `负责人账号`; map it to `ProjectMaster` fields and assert successful preview/confirm. Add a case with `BlankMeansNoChange = true` and verify an empty mapped cell preserves the existing value.

- [ ] **Step 2: Write failing attachment import tests**

Export a project ZIP, import it into a fresh test database/file store, and assert attachment bytes, metadata, relation, size, and hash match. Add checksum mismatch, path traversal, missing file, and duplicate relative-path cases; each must block the whole batch.

- [ ] **Step 3: Run mapped/attachment tests and verify RED**

Run the Task 6 command. Expected: new mapping and ZIP assertions fail.

- [ ] **Step 4: Implement per-sheet mapping**

Accept source-to-target mappings keyed by `ProjectWorkbookSheet`. Require a target sheet for arbitrary files, reject duplicate target fields, apply catalog aliases before explicit mappings, and persist mapping JSON with the import batch. Apply `BlankMeansNoChange` only to mapped arbitrary files.

- [ ] **Step 5: Implement safe ZIP parsing and attachment staging**

Reject absolute paths, `..`, duplicate normalized paths, unsupported extensions, oversized entries, missing manifest/checksum data, and hash/size mismatches. Stage bytes in memory or temporary files until business validation passes; save through `IFileStore` only inside confirm. On database or file failure, delete newly saved files and roll back the database transaction.

- [ ] **Step 6: Run import tests and verify GREEN**

Run the Task 6 command. Expected: all project workbook import tests pass.

---

### Task 8: Add project list/detail and data-exchange UI workflows

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/DataExchange/DataExchangeLabels.cs`
- Modify: `src/EngineeringManager.Web/wwwroot/js/components/check-selector.js`
- Modify: `src/EngineeringManager.Web/wwwroot/css/components.css`
- Test: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/DataExchangeBackupAuthorizationTests.cs`

- [ ] **Step 1: Write failing page and authorization tests**

Assert the project list renders row checkboxes, “select all matching”, project workbook sheet choices, selected/matching counts, and a separate `ExportWorkbook` handler. Assert project detail exports only the current authorized project. Assert data exchange renders standard/mapped/ZIP import controls and per-sheet preview. Assert query-only users cannot import and unauthorized manual IDs never reach output.

- [ ] **Step 2: Run web tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~DataExchangeBackupAuthorizationTests"
```

Expected: new markup/handler assertions fail.

- [ ] **Step 3: Add project list selection and workbook export**

Bind `SelectedProjectIds`, `SelectAllMatching`, `SelectedWorkbookSheets`, `IncludeWorkbookAttachments`, and `WorkbookCutoffDate`. Reuse the current filter query and actor. Keep the existing `Export` handler unchanged; add `ExportWorkbook` for the multi-sheet flow. Render stable-size row checkboxes and a compact sheet selector with all sheets selected by default.

- [ ] **Step 4: Add current-project detail export**

Render an export menu on project details and post current project ID plus selected sheets to an authorization-checked handler. Use the same service and workbook format as list exports.

- [ ] **Step 5: Add project workbook controls to data exchange**

Expose project sheet catalog, filters/manual IDs, standard/mapped/ZIP upload modes, import mode, mapping JSON, blank behavior, preview sheet counts/errors, and confirm. Enforce admin/application-admin import at the page boundary and service authorization scope at export.

- [ ] **Step 6: Add focused JavaScript/CSS**

Update selection counts, keep explicit IDs separate from `SelectAllMatching`, and preserve form values. Use existing button, dropdown, checkbox, table, and responsive patterns; do not add a new design system or page-wide card layout.

- [ ] **Step 7: Run web tests and verify GREEN**

Run the Task 8 command. Expected: new and existing authorization/page tests pass.

---

### Task 9: Regression, performance, and documentation verification

**Files:**
- Modify only as failures require within Task 1-8 files.
- Verify: `docs/superpowers/specs/2026-07-19-project-management-workbook-data-exchange-design.md`
- Verify: `docs/superpowers/plans/2026-07-19-project-management-workbook-data-exchange.md`

- [ ] **Step 1: Run all focused data-exchange and project tests**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectWorkbook|FullyQualifiedName~SimpleXlsxWorkbook|FullyQualifiedName~ProjectOverviewExportTests|FullyQualifiedName~ModuleExportTests|FullyQualifiedName~StandardImportTests|FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~DataExchangeBackupAuthorizationTests"
```

Expected: all selected tests pass with zero failures.

- [ ] **Step 2: Run Release build**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 build EngineeringManager.sln -c Release --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Step 3: Run the complete test suite**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/dotnet.ps1 test EngineeringManager.sln -c Release --no-build
```

Expected: all tests pass.

- [ ] **Step 4: Run the repository quality gate**

```powershell
$ErrorActionPreference = 'Stop'
./scripts/quality-gate.ps1
```

Expected: quality gate exits successfully.

- [ ] **Step 5: Review requirement coverage and working tree**

Verify every specification section maps to a passing test or explicit UI behavior. Run:

```powershell
$ErrorActionPreference = 'Stop'
git status --short
git diff --check
```

Expected: no whitespace errors; unrelated user changes remain preserved; no commit, branch, push, migration, or production database operation occurred.
