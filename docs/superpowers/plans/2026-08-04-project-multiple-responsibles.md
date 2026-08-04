# Project Multiple Responsible Employees Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with review checkpoints.

**Goal:** Add normalized multi-responsible employee support and import all historical project manager assignments into the test database.

**Architecture:** Add a `ProjectResponsibleEmployee` join entity with order and primary flags while keeping `Projects.ResponsibleEmployeeId` as a compatibility projection. Thread ordered employee IDs through application services and Razor Pages, render names joined by `、`, and run a backed-up, transactional, auditable maintenance backfill.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQL Server migrations, C# records/services, xUnit/FluentAssertions, PowerShell/sqlcmd maintenance verification.

---

### Task 1: Add failing domain and application tests

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/ProjectModelTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ChineseDisplayTests.cs`

- [ ] Add tests proving a project can hold two ordered employee responsibility links, options only include active eligible employees, update validation accepts both IDs, the first ID remains the primary projection, workbook round-trips preserve the primary field, and the edit/detail pages expose multi-responsible markup.
- [ ] Run the focused test classes and confirm the new assertions fail because the join entity and collection contracts do not exist yet.

### Task 2: Add the normalized persistence model and migration

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ProjectResponsibleEmployee.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Project.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: generated migration under `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/`

- [ ] Add the entity with `Id`, `ProjectId`, `EmployeeId`, `SortOrder`, `IsPrimary`, timestamps, and concurrency stamp.
- [ ] Configure the unique project/employee index, ordered index, and restrictive employee delete behavior; expose `Project.ResponsibleEmployees`.
- [ ] Generate the EF migration without deleting `ResponsibleEmployeeId`; include a data migration that seeds one relationship from every existing non-null primary projection.
- [ ] Run infrastructure tests and inspect the generated migration before applying it.

### Task 3: Thread multi-responsible contracts through application services

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectListWorkbookExporter.cs`

- [ ] Add ordered `ResponsibleEmployeeIds`/`ResponsibleEmployeeNames` projections and keep singular fields as the primary compatibility projection.
- [ ] Centralize validation for active eligible employees, deduplicate IDs, and synchronize the join rows plus the primary projection inside the existing project transaction.
- [ ] Update list filtering and search to match any linked responsible employee.
- [ ] Keep legacy workbook `responsible_employee_id` import/export working and add a delimited multi-ID/name representation for round-trip data.
- [ ] Run application tests and fix all contract compilation failures.

### Task 4: Update Razor Pages for multiple responsibility

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Edit.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Edit.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] Replace the responsible employee single select with an accessible multi-select and preserve the primary ordering.
- [ ] Populate selected IDs on edit and quick-edit postbacks; show `未设置` only when the collection is empty.
- [ ] Render multiple names consistently in the project title, detail overview, list table, filters, and exports.
- [ ] Run web tests and browser/health checks against the running local service.

### Task 5: Run the backed-up historical backfill

**Files:**
- Create: `EngineeringManager/scripts/backfill-project-responsible-employees.ps1`
- Create: `EngineeringManager/scripts/README.md` (maintenance command usage and rollback notes)

- [ ] Add a maintenance command that loads the extracted source mapping, canonicalizes annotations, allocates deterministic `YG####` numbers, creates missing employees, creates all relationship rows, updates only empty primary projections, and inserts JSON audit records in one transaction.
- [ ] Create a SQL Server full backup before schema/data writes and record its path in the command output.
- [ ] Run the command against `EngineeringManager_Test`, then run SQL verification for expected employee names, 64 source projects, multi-responsible rows, primary projections, and audit batch count.

### Task 6: Final verification

**Files:**
- No new source files.

- [ ] Run the focused tests, full test suite, and `dotnet build` using the repository runtime.
- [ ] Confirm the local service remains healthy at `/health/live` and `/health/ready`.
- [ ] Review `git diff` and preserve the user's pre-existing page/CSS/build changes.
