# Project Overview Unified Quantity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify project quantity data to one value set, drive amount interpretation from project stage, centralize finance years, compact the overview, and add record-scoped attachments and editors.

**Architecture:** Keep existing project and central-ledger identities stable while replacing dual quantity columns with `Quantity`, `UnitPrice`, `AccountingLabel`, and `RequiresInvoice`. Extend the shared attachment entity with explicit nullable foreign keys for the five supported business record types and expose attachment operations through one project-scoped service. Use dedicated project-scoped editor routes so detailed editing cannot mutate overview fields.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 10, SQL Server, xUnit, FluentAssertions, existing local file store and CSS/JavaScript assets.

---

### Task 1: Lock unified project-stage and amount rules

**Files:**
- Modify: `src/EngineeringManager.Domain/Projects/ProjectStage.cs`
- Modify: `src/EngineeringManager.Domain/Projects/ProjectAmountCalculator.cs`
- Modify: `tests/EngineeringManager.Tests/Domain/ProjectAmountCalculatorTests.cs`

- [ ] Add failing tests proving `PartiallySettled` exists, partial/final stages use the same quantity amount as settlement, other stages use it as estimated, and invoice-required totals exclude opted-out lines.
- [ ] Run `pwsh -NoProfile -File scripts/dotnet.ps1 test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj -c Release --filter FullyQualifiedName~ProjectAmountCalculatorTests` and confirm the new tests fail for missing APIs.
- [ ] Add the stage value and a minimal calculator result containing total amount, interpreted estimated/settled amount, and invoice-required amount.
- [ ] Re-run the filtered tests and confirm they pass.

### Task 2: Replace dual contract-line values and migrate data

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/Data/ContractLineItem.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Modify: `src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectSummaryService.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_UnifiedProjectQuantities.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- Modify: `tests/EngineeringManager.Tests/Infrastructure/ProjectModelTests.cs`
- Modify: `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`

- [ ] Add failing model and service tests for `Quantity`, `UnitPrice`, `AccountingLabel`, `RequiresInvoice`, default invoice requirement, and stage-based summaries.
- [ ] Run the two filtered test classes and confirm failures refer to the removed/missing unified fields.
- [ ] Implement the new entity, DTO and service contracts; keep the line-item ID and existing relationships unchanged.
- [ ] Generate an EF migration that selects settled values for partially/finally settled projects, estimated values otherwise, falls back to the other pair when null, initializes `RequiresInvoice = 1`, then drops the old columns.
- [ ] Re-run model and service tests and inspect the generated SQL operations.

### Task 3: Update downstream finance, dashboard, sample and workbook consumers

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`
- Modify: relevant tests under `tests/EngineeringManager.Tests/Application`

- [ ] Add failing tests for unified workbook columns, stage-based summaries, and total invoice-required amount.
- [ ] Run the affected filtered tests and confirm they fail on the old dual-field contract.
- [ ] Replace all old estimated/settled quantity consumers with unified fields and project-stage interpretation.
- [ ] Re-run the affected tests and confirm passing results.

### Task 4: Centralize finance-year administration

**Files:**
- Create: `src/EngineeringManager.Web/Pages/Admin/FinanceYears/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Admin/FinanceYears/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Admin/Index.cshtml`
- Modify: navigation files that link to `Pages/Ledger/Years`
- Modify: `tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/AdminAuthorizationTests.cs`

- [ ] Add failing page tests requiring the system-settings finance-year route and absence of the ledger maintenance link.
- [ ] Run the filtered web tests and confirm failure.
- [ ] Move the existing page behavior to the admin route while retaining the shared `FinanceBusinessYear` service and data.
- [ ] Re-run the filtered tests and confirm passing results.

### Task 5: Add record-scoped attachment relationships and service

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/Data/Attachment.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `src/EngineeringManager.Application/Projects/ProjectRecordAttachmentDtos.cs`
- Create: `src/EngineeringManager.Application/Projects/IProjectRecordAttachmentService.cs`
- Create: `src/EngineeringManager.Infrastructure/Projects/ProjectRecordAttachmentService.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`
- Create: EF migration and snapshot updates
- Create: `tests/EngineeringManager.Tests/Application/ProjectRecordAttachmentServiceTests.cs`

- [ ] Write failing tests for multiple attachments on quantity, collection, invoice, payment, and construction records; verify project scope and permissions; verify safe download metadata and deletion.
- [ ] Run the new test class and confirm missing-service failures.
- [ ] Add explicit nullable foreign keys and one-of-five validation, implement upload/list/download/delete with the existing file store, and register the service.
- [ ] Re-run the new tests and confirm passing results.

### Task 6: Build isolated detailed editors

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Contracts/Edit.cshtml*`
- Create: `src/EngineeringManager.Web/Pages/Projects/Collections/Edit.cshtml*`
- Create: `src/EngineeringManager.Web/Pages/Projects/Invoices/Edit.cshtml*`
- Create: `src/EngineeringManager.Web/Pages/Projects/Payments/Edit.cshtml*`
- Create: `src/EngineeringManager.Web/Pages/Projects/Construction/Edit.cshtml*`
- Create shared attachment partials under `src/EngineeringManager.Web/Pages/Projects/Shared`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] Add failing authorization and markup tests proving each route is project-scoped, edits only its record type, and exposes attachment upload controls.
- [ ] Run the filtered web tests and confirm failure.
- [ ] Implement the five editors and shared attachment partials without overview input bindings.
- [ ] Re-run the web tests and confirm passing results.

### Task 7: Compact project details and unify the quantity table

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: relevant project page tests

- [ ] Add failing markup tests for the partial-settlement label, single quantity/price/subtotal columns, accounting label, manual invoice requirement, attachment count, merged contact display, amount selector, and half-width tax/equipment row.
- [ ] Run the filtered tests and confirm failure.
- [ ] Implement the unified markup and handlers; reduce desktop overview card/panel height to about two-thirds using scoped CSS; point detailed-edit buttons to isolated routes.
- [ ] Re-run the project page tests and confirm passing results.

### Task 8: Full verification and desktop acceptance

**Files:**
- Modify documentation only if behavior references require updating.

- [ ] Run the focused domain, model, application and web tests introduced above.
- [ ] Run `pwsh -NoProfile -File scripts/dotnet.ps1 test EngineeringManager.sln -c Release` and require zero failures.
- [ ] Run `pwsh -NoProfile -File scripts/dotnet.ps1 build EngineeringManager.sln -c Release --no-restore` and require zero errors and zero warnings.
- [ ] Start the app using the repository PowerShell workflow and verify the project details and finance-year pages at desktop width.
- [ ] Confirm no page-level horizontal overflow, no browser console errors, correct scoped editors, attachment operations, and correct totals. Do not perform mobile acceptance.
