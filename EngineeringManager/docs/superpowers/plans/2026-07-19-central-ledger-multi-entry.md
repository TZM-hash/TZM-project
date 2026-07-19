# Central Ledger Multi-Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execute serially in the current workspace because the finance contracts, migration, project workbook, and page models share evolving types.

**Goal:** Build one central financial ledger shared by project, construction-crew, business-partner, external-ledger, and internal-ledger entry points, with complex settlement/invoice/cash statistics, independent finance years, editable reconciliation snapshots, deletion audit, and verified legacy-data migration.

**Architecture:** Add a new central-ledger model beside the current project-centric finance tables. Route new writes through focused command, allocation, query, posting, year, and reconciliation services; keep `IFinanceLedgerService` as a compatibility adapter until every existing page and the uncommitted project workbook use the new services. Back up and migrate only `EngineeringManager_Test`, reconcile old/new totals, then stop old-table writes while retaining old tables and stable ID mappings for verification.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 10, SQL Server/SQLite, C# 14, xUnit, FluentAssertions, existing audit/authorization/data-workbench/XLSX infrastructure, PowerShell 7.

**Source specification:** `docs/superpowers/specs/2026-07-19-central-ledger-multi-entry-design.md`

**Execution status (2026-07-20):** Implemented and locally accepted. The checkbox list below is retained as the original execution runbook; authoritative observed results, migration reconciliation, browser QA, and release evidence are recorded in the workspace-level `docs/开发进度.md` and `docs/release-checklist.md`.

**Execution constraints:**

- Preserve every existing uncommitted project-workbook change.
- Do not inspect, modify, import, move, or delete `../old-data/`.
- Do not create branches, commits, pushes, stashes, or rewrite Git history.
- Do not connect to or mutate a production database.
- Before applying the new migration, stop and obtain explicit confirmation that the fresh `EngineeringManager_Test` backup succeeded.
- Do not reset `EngineeringManager_Test`; the reset script still removes the non-demo administrator. Apply migrations in place after backup.
- Every production-code change follows RED → GREEN → REFACTOR. Record the observed failing and passing commands in this plan.
- Project, crew, partner, external-ledger, and internal-ledger entry points must always read and write the same underlying central records; no synchronization copy is permitted.

---

## File structure

### Create: domain and application contracts

- `src/EngineeringManager.Domain/Finance/CentralLedgerEnums.cs` — scope, direction, source, settlement, cash, adjustment, allocation, and reconciliation enums.
- `src/EngineeringManager.Domain/Finance/CentralLedgerCalculator.cs` — pure per-settlement calculation and aggregate addition.
- `src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs` — actors, requests, details, filters, metrics, rows, and option DTOs.
- `src/EngineeringManager.Application/Finance/ICentralLedgerCommandService.cs` — create, update, finalize, delete, void, reverse, and allocation commands.
- `src/EngineeringManager.Application/Finance/ICentralLedgerQueryService.cs` — overview, list, details, options, project, crew, partner, and internal-ledger queries.
- `src/EngineeringManager.Application/Finance/IFinancePostingService.cs` — idempotent business-source posting API for project quantity, crew, and partner entry points.
- `src/EngineeringManager.Application/Finance/IFinanceBusinessYearService.cs` — finance-only year management.
- `src/EngineeringManager.Application/Finance/IFinanceReconciliationService.cs` — snapshot, comparison, and history API.

### Create: persistence

- `src/EngineeringManager.Infrastructure/Data/FinanceSettlement.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceSettlementAdjustment.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceDeduction.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceInvoice.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceInvoiceAllocation.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceCashEntry.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceCashAllocation.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceBusinessYear.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceReconciliation.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceReconciliationLine.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceDeletionLog.cs`
- `src/EngineeringManager.Infrastructure/Data/FinanceLegacyMap.cs`

### Create: infrastructure services

- `src/EngineeringManager.Infrastructure/Finance/CentralLedgerCommandService.cs`
- `src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`
- `src/EngineeringManager.Infrastructure/Finance/CentralLedgerAllocationService.cs`
- `src/EngineeringManager.Infrastructure/Finance/FinancePostingService.cs`
- `src/EngineeringManager.Infrastructure/Finance/FinanceBusinessYearService.cs`
- `src/EngineeringManager.Infrastructure/Finance/FinanceReconciliationService.cs`
- `src/EngineeringManager.Infrastructure/Finance/LegacyFinanceMigrationService.cs`

### Create: Razor Pages and assets

- `src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/Years/Index.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/Years/Index.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/Reconciliations/Index.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/Reconciliations/Index.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/Reconciliations/Details.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/Reconciliations/Details.cshtml.cs`
- `src/EngineeringManager.Web/Pages/Ledger/_LedgerMetrics.cshtml`
- `src/EngineeringManager.Web/Pages/Ledger/_LedgerFilters.cshtml`
- `src/EngineeringManager.Web/Pages/Partners/Details.cshtml`
- `src/EngineeringManager.Web/Pages/Partners/Details.cshtml.cs`
- `src/EngineeringManager.Web/wwwroot/js/components/collapsible-nav.js`

### Create: tests

- `tests/EngineeringManager.Tests/Domain/CentralLedgerCalculatorTests.cs`
- `tests/EngineeringManager.Tests/Infrastructure/CentralLedgerModelTests.cs`
- `tests/EngineeringManager.Tests/Application/CentralLedgerCommandServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/CentralLedgerAllocationServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/FinancePostingServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/FinanceBusinessYearServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/FinanceReconciliationServiceTests.cs`
- `tests/EngineeringManager.Tests/Application/LegacyFinanceMigrationTests.cs`
- `tests/EngineeringManager.Tests/Application/ProjectWorkbookCentralLedgerTests.cs`
- `tests/EngineeringManager.Tests/Web/CentralLedgerAuthorizationTests.cs`
- `tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`

### Modify

- `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs` — DbSets, relationships, constraints, precision, and indexes.
- `src/EngineeringManager.Domain/Security/PermissionKeys.cs` — external/internal ledger, year, and reconciliation permissions.
- `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs` — compatibility adapter over central services.
- `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs` and `FinanceDtos.cs` — preserve old call sites while delegating.
- `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs` — post project quantity receivables idempotently.
- `src/EngineeringManager.Infrastructure/ConstructionCrews/ConstructionCrewService.cs` and related DTO/interface files — show and create crew payables, input invoices, payments, and deductions.
- `src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs` and related DTO/interface files — show and create partner payables, input invoices, payments, and deductions.
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookCatalog.cs`, `ProjectWorkbookExporter.cs`, `ProjectWorkbookImporter.cs`, `ProjectWorkbookService.cs` — use central records and allocations.
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml` and `.cs` — central receivable/output-invoice/collection/payment entry points.
- `src/EngineeringManager.Web/Pages/Crews/Details.cshtml` and `.cs` — crew finance tabs and commands.
- `src/EngineeringManager.Web/Pages/Partners/Index.cshtml` and `.cs` — link each partner to the new details page and preserve existing list/create behavior.
- `src/EngineeringManager.Web/Pages/Finance/*` — redirect legacy finance routes to `/Ledger/External` or delegate entry handlers.
- `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml` — collapsible Central Ledger menu.
- `src/EngineeringManager.Web/Program.cs` — registrations.
- `src/EngineeringManager.Web/wwwroot/css/components.css`, `pages.css`, and `site.js` — ledger workbench and menu behavior.
- `src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs` — central-ledger sample data without duplicating old writes.
- `scripts/verify-backup-restore.ps1` — central-table verification after migration.
- `docs/开发进度.md`, `README.md`, and `docs/release-checklist.md` — observed implementation and deployment facts.

---

## Phase 1 — Central ledger foundation

### Task 1: Establish baseline and calculation behavior matrix

**Files:**
- Create: `tests/EngineeringManager.Tests/Domain/CentralLedgerCalculatorTests.cs`
- Reference: `src/EngineeringManager.Domain/Finance/LedgerCalculator.cs`
- Reference: `docs/superpowers/specs/2026-07-19-central-ledger-multi-entry-design.md`

- [ ] **Step 1: Capture the untouched baseline**

```powershell
$ErrorActionPreference = 'Stop'
git status --short
& .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release --no-build
```

Expected: the known project-workbook files remain modified/untracked; the existing full suite passes with zero failures.

- [ ] **Step 2: Write the failing calculation tests**

Create tests using this desired API:

```csharp
var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
    GrossSettlementAmount: 1_000_000m,
    Deductions: 100_000m,
    InvoiceReducingDeductions: 0m,
    BaseInvoiceAmount: 1_000_000m,
    InvoicedAmount: 600_000m,
    CashAmount: 800_000m));

result.ActualAmount.Should().Be(900_000m);
result.UncollectedOrUnpaid.Should().Be(100_000m);
result.AdvanceInvoiceCash.Should().Be(200_000m);
result.UninvoicedAndUncollectedOrUnpaid.Should().Be(100_000m);
```

Add separate facts for deduction reducing invoice, deduction not reducing invoice, invoiced-without-cash-requirement, over-settlement cash, over-invoicing, payable deductions excluded from cash paid, and aggregate addition.

- [ ] **Step 3: Run the tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerCalculatorTests'
```

Expected: compilation fails because `CentralLedgerCalculator`, `CentralLedgerCalculationInput`, and `CentralLedgerMetrics` do not exist.

- [ ] **Step 4: Record the RED output in the plan checkbox note**

Do not add production code until the failure is confirmed to be caused by the missing central-ledger API.

### Task 2: Implement domain enums and calculator

**Files:**
- Create: `src/EngineeringManager.Domain/Finance/CentralLedgerEnums.cs`
- Create: `src/EngineeringManager.Domain/Finance/CentralLedgerCalculator.cs`
- Test: `tests/EngineeringManager.Tests/Domain/CentralLedgerCalculatorTests.cs`

- [ ] **Step 1: Add stable enums**

```csharp
public enum LedgerScope { External = 1, Internal = 2 }
public enum LedgerDirection { Receivable = 1, Payable = 2 }
public enum LedgerSettlementState { Provisional = 1, Final = 2 }
public enum LedgerSourceType { ProjectQuantity = 1, Crew = 2, Partner = 3, CentralLedger = 4, LegacyMigration = 5 }
public enum LedgerAdjustmentType { FinalSettlement = 1, Correction = 2, Reversal = 3 }
public enum LedgerCashType { Collection = 1, Payment = 2, InternalTransfer = 3 }
public enum LedgerRecordStatus { Active = 1, Voided = 2 }
public enum LedgerAllocationStatus { Unallocated = 1, PartiallyAllocated = 2, FullyAllocated = 3 }
public enum FinanceRecordType { Settlement = 1, Deduction = 2, Invoice = 3, Cash = 4, Adjustment = 5 }
public enum FinanceReconciliationScope { WholeLedger = 1, LegalEntity = 2, BusinessPartner = 3 }
```

- [ ] **Step 2: Implement the pure calculator**

```csharp
public sealed record CentralLedgerCalculationInput(
    decimal GrossSettlementAmount,
    decimal Deductions,
    decimal InvoiceReducingDeductions,
    decimal BaseInvoiceAmount,
    decimal InvoicedAmount,
    decimal CashAmount);

public sealed record CentralLedgerMetrics(
    decimal GrossSettlementAmount,
    decimal Deductions,
    decimal ActualAmount,
    decimal ShouldInvoiceAmount,
    decimal InvoicedAmount,
    decimal CashAmount,
    decimal UncollectedOrUnpaid,
    decimal Uninvoiced,
    decimal InvoicedAndCollectedOrPaid,
    decimal InvoicedAndUncollectedOrUnpaid,
    decimal AdvanceInvoiceCash,
    decimal UninvoicedAndUncollectedOrUnpaid,
    decimal InvoicedWithoutCashRequirement,
    decimal OverSettlementCash,
    decimal OverInvoiced)
{
    public static CentralLedgerMetrics Zero => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
```

`Calculate` must reject negative input components, compute the exact formulas from the specification, and `Add` must sum every property without recomputing at aggregate level.

- [ ] **Step 3: Run domain tests and verify GREEN**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerCalculatorTests|FullyQualifiedName~LedgerCalculatorTests'
```

Expected: all selected tests pass; existing ledger-calculator tests remain green.

- [ ] **Step 4: Checkpoint without Git history changes**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: no whitespace errors and no unrelated file changes.

### Task 3: Define central persistence model

**Files:**
- Create: the twelve persistence files listed in File structure.
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Test: `tests/EngineeringManager.Tests/Infrastructure/CentralLedgerModelTests.cs`

- [ ] **Step 1: Write failing SQLite model tests**

Test that an external receivable settlement, deduction, output invoice, collection, allocations, finance year, reconciliation line, deletion log, and legacy map persist in one SQLite transaction. Add tests for:

```csharp
settlement.Scope.Should().Be(LedgerScope.External);
settlement.Direction.Should().Be(LedgerDirection.Receivable);
settlement.ProjectId.Should().NotBeNull();
invoice.Allocations.Single().SettlementId.Should().Be(settlement.Id);
cash.Allocations.Single().SettlementId.Should().Be(settlement.Id);
```

Also assert external records require `BusinessPartnerId`, internal records require `CounterLegalEntityId`, finance years reject overlap through a unique service rule, and deleting a settlement does not cascade to invoice or cash headers.

- [ ] **Step 2: Run model tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerModelTests'
```

Expected: compilation fails because the central entities and DbSets do not exist.

- [ ] **Step 3: Implement entity fields and relationships**

Use these required entity cores:

```csharp
public sealed class FinanceSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerScope Scope { get; set; }
    public LedgerDirection Direction { get; set; }
    public LedgerSettlementState SettlementState { get; set; }
    public LedgerSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ContractLineItemId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal OriginalInvoiceAmount { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
```

`FinanceSettlementAdjustment` stores `AmountDelta`, `InvoiceAmountDelta`, type, reason, date, actor, and source. `FinanceDeduction` stores `Amount` and `ReduceInvoiceAmount`. Invoice and cash headers contain parties and effective status; allocation rows contain settlement, project/contract snapshots, amount, and order. Deletion logs contain immutable JSON snapshots and before/after metric JSON.

- [ ] **Step 4: Configure precision, constraints, and indexes**

Add all DbSets and configure `decimal(18,2)`, `DeleteBehavior.Restrict` for business headers, `DeleteBehavior.Cascade` only from invoice/cash headers to their allocation rows, and indexes for:

```text
FinanceSettlements: Scope + Direction + LegalEntityId + BusinessDate
FinanceSettlements: BusinessPartnerId + BusinessDate
FinanceSettlements: ProjectId + BusinessDate
FinanceSettlements: SourceType + SourceId (unique when SourceId is not null)
FinanceInvoices: LegalEntityId + InvoiceNumber + InvoiceDate
FinanceCashEntries: LegalEntityId + BusinessDate
FinanceBusinessYears: StartDate + EndDate
FinanceLegacyMaps: LegacyEntityType + LegacyId (unique)
```

- [ ] **Step 5: Run model tests and existing finance model tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerModelTests|FullyQualifiedName~FinanceModelTests'
```

Expected: all selected tests pass with SQLite foreign-key enforcement enabled.

### Task 4: Define application contracts and test fixture

**Files:**
- Create: the five application interface/DTO files listed above.
- Create: `tests/EngineeringManager.Tests/Application/CentralLedgerTestFixture.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`

- [ ] **Step 1: Add DTO compile tests through command-service test setup**

The desired request shape is:

```csharp
public sealed record CentralLedgerActor(
    string UserId,
    string? UserName,
    IReadOnlySet<Guid> LegalEntityIds,
    IReadOnlySet<Guid> ProjectIds,
    bool CanManageExternal,
    bool CanManageInternal,
    bool CanManageYears,
    bool CanReconcile);

public sealed record CreateSettlementRequest(
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerSettlementState SettlementState,
    LedgerSourceType SourceType,
    Guid? SourceId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    Guid? ContractLineItemId,
    DateOnly BusinessDate,
    decimal OriginalAmount,
    decimal OriginalInvoiceAmount,
    string? Notes);
```

Define corresponding requests for finalization, deduction, invoice, cash, allocation replacement, physical delete, query, year, and reconciliation.

- [ ] **Step 2: Define focused service boundaries**

```csharp
public interface ICentralLedgerCommandService
{
    Task<Guid> CreateSettlementAsync(CentralLedgerActor actor, CreateSettlementRequest request, CancellationToken token);
    Task FinalizeSettlementAsync(CentralLedgerActor actor, FinalizeSettlementRequest request, CancellationToken token);
    Task<Guid> AddDeductionAsync(CentralLedgerActor actor, AddFinanceDeductionRequest request, CancellationToken token);
    Task<Guid> CreateInvoiceAsync(CentralLedgerActor actor, CreateFinanceInvoiceRequest request, CancellationToken token);
    Task<Guid> CreateCashAsync(CentralLedgerActor actor, CreateFinanceCashRequest request, CancellationToken token);
    Task ReplaceInvoiceAllocationsAsync(CentralLedgerActor actor, ReplaceInvoiceAllocationsRequest request, CancellationToken token);
    Task ReplaceCashAllocationsAsync(CentralLedgerActor actor, ReplaceCashAllocationsRequest request, CancellationToken token);
    Task DeleteAsync(CentralLedgerActor actor, DeleteFinanceRecordRequest request, CancellationToken token);
}
```

Define the query boundary explicitly:

```csharp
public interface ICentralLedgerQueryService
{
    Task<CentralLedgerOverviewPageDto> SearchAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token);
    Task<CentralLedgerDetailsDto?> GetAsync(CentralLedgerActor actor, FinanceRecordType type, Guid id, CancellationToken token);
    Task<CentralLedgerOptionsDto> GetOptionsAsync(CentralLedgerActor actor, LedgerScope scope, CancellationToken token);
    Task<CentralLedgerMetrics> GetProjectMetricsAsync(CentralLedgerActor actor, Guid projectId, CancellationToken token);
    Task<CentralLedgerMetrics> GetPartnerMetricsAsync(CentralLedgerActor actor, Guid businessPartnerId, CancellationToken token);
}
```

Create separate posting, year, and reconciliation interfaces; do not add all behavior back into the existing `IFinanceLedgerService`.

- [ ] **Step 3: Add deterministic SQLite fixture**

The fixture creates one legal entity, a second internal legal entity, one project/contract/line item, a client partner, a supplier partner, a crew-role partner, and two accounts. Expose helper methods for external actors, internal actors, and read-only actors.

- [ ] **Step 4: Register service placeholders only after concrete classes exist**

Do not change `Program.cs` to reference missing classes. Registration occurs in the task that creates each implementation.

## Phase 2 — Commands, allocations, entry integration, and migration

### Task 5: Implement settlement, finalization, deduction, and deletion commands

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/CentralLedgerCommandService.cs`
- Test: `tests/EngineeringManager.Tests/Application/CentralLedgerCommandServiceTests.cs`

- [ ] **Step 1: Write failing command tests**

Add one behavior per fact:

```csharp
CreateProjectReceivableIsFormalImmediately();
FinalizingProvisionalSettlementAddsTraceableDelta();
DeductionAlwaysReducesActualAmount();
DeductionOptionControlsInvoiceReduction();
CrewPaymentDeductionIsNotCashPaid();
DeletingSettlementDetachesAllocationsAndLeavesHeaders();
DeletingDeductionRestoresActualAndInvoiceMetrics();
DeleteRequiresReasonAndWritesImmutableSnapshot();
StaleConcurrencyStampRejectsUpdateAndDelete();
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerCommandServiceTests'
```

Expected: failures identify the unimplemented command service.

- [ ] **Step 3: Implement validation and transactional commands**

Each command must validate actor scope, external/internal party shape, project/contract relationships, positive amounts, required reason, and concurrency. Use one EF transaction for record changes, audit log, deletion log, allocation detachment, and account projections.

Finalization adds a `FinanceSettlementAdjustment`:

```csharp
var currentAmount = settlement.OriginalAmount + settlement.Adjustments.Sum(x => x.AmountDelta);
var currentInvoiceAmount = settlement.OriginalInvoiceAmount + settlement.Adjustments.Sum(x => x.InvoiceAmountDelta);
adjustment.AmountDelta = request.FinalAmount - currentAmount;
adjustment.InvoiceAmountDelta = request.FinalInvoiceAmount - currentInvoiceAmount;
settlement.SettlementState = LedgerSettlementState.Final;
```

- [ ] **Step 4: Implement physical delete behavior**

Before removal, serialize the header, adjustments, deductions, allocations, related IDs, and calculated before metrics. Remove only the selected header and its allocations; detach opposite headers; recalculate after metrics; persist `FinanceDeletionLog` and existing `AuditLog` in the same transaction.

- [ ] **Step 5: Run command and calculator tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerCommandServiceTests|FullyQualifiedName~CentralLedgerCalculatorTests'
```

Expected: all selected tests pass.

### Task 6: Implement invoice/cash allocation and FIFO matching

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/CentralLedgerAllocationService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/CentralLedgerCommandService.cs`
- Test: `tests/EngineeringManager.Tests/Application/CentralLedgerAllocationServiceTests.cs`

- [ ] **Step 1: Write failing allocation tests**

Cover invoice and cash separately:

```csharp
ManualInvoiceCanAllocateAcrossProjectsAndSettlements();
ManualCashCanAllocateAcrossProjectsAndSettlements();
AutomaticInvoiceUsesOldestUninvoicedSettlementFirst();
AutomaticCashUsesOldestUnsettledCashBalanceFirst();
AutomaticMatchingNeverCrossesLegalEntityOrCounterparty();
RemainderStaysUnallocated();
ReplacingAllocationsRecalculatesEveryAffectedSummary();
AllocationCannotExceedHeaderEffectiveAmount();
```

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerAllocationServiceTests'
```

- [ ] **Step 3: Implement the allocation engine**

The engine receives header amount, manual allocations, legal entity, counterparty, scope, and direction. If manual allocations are empty, query eligible settlements ordered by `BusinessDate`, then `Id`, and allocate the minimum of remaining header amount and remaining invoice/cash capacity. Persist project and contract snapshots from the target settlement.

- [ ] **Step 4: Preserve separate anomaly indicators**

Do not cap allocated cash at settlement or invoice values. The calculator must expose both `AdvanceInvoiceCash = max(C - I, 0)` and `OverSettlementCash = max(C - N, 0)`.

- [ ] **Step 5: Run allocation, command, and old finance tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerAllocationServiceTests|FullyQualifiedName~CentralLedgerCommandServiceTests|FullyQualifiedName~FinanceLedgerServiceTests|FullyQualifiedName~FinanceSummaryTests'
```

Expected: central tests pass and current finance behavior remains green.

### Task 7: Implement query service and compatibility adapter

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify: `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Test: `tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`
- Test: existing finance service/summary tests.

- [ ] **Step 1: Write failing query tests**

Test database-side filtering by scope, legal entity, project, contract, crew partner, ordinary partner, finance year/date, provisional/final, settlement state, invoice state, allocation state, anomaly flags, and whitespace-separated full-field search.

Assert totals are calculated from authorized filtered settlement details, not from the current page only.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerQueryServiceTests'
```

- [ ] **Step 3: Implement database projection and per-detail aggregation**

Use EF queries to load only filtered settlement IDs and grouped effective adjustment/deduction/invoice/cash sums. Calculate each detail using `CentralLedgerCalculator`, then aggregate with `CentralLedgerCalculator.Add`.

Return `CentralLedgerOverviewPageDto` containing rows, totals, page metadata, available columns, and matching IDs.

- [ ] **Step 4: Convert existing finance service into an adapter**

Preserve existing method signatures used by project pages and the uncommitted workbook. Map old create/update requests to central command requests and old summaries to central query metrics. Do not write `ReceivableEntries`, `PayableEntries`, `CollectionEntries`, `PaymentEntries`, or `InvoiceEntries` after the adapter switch.

- [ ] **Step 5: Run central and compatibility tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerQueryServiceTests|FullyQualifiedName~FinanceLedgerServiceTests|FullyQualifiedName~FinanceSummaryTests|FullyQualifiedName~ProjectWorkspaceServiceTests'
```

Expected: all selected tests pass.

### Task 8: Add idempotent business posting and multi-entry pages

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/FinancePostingService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: construction-crew and partner services/DTOs/interfaces.
- Modify: project, crew, and partner Razor Pages.
- Test: `tests/EngineeringManager.Tests/Application/FinancePostingServiceTests.cs`
- Test: project/crew/partner web tests.

- [ ] **Step 1: Write failing posting tests**

```csharp
ConfirmedProjectQuantityUpsertsOneReceivableBySourceId();
EditingQuantityUpdatesTheSameCentralSettlement();
UnconfirmedQuantityUsesProvisionalAmount();
ProjectCollectionPostsCashNotReceivable();
ProjectPaymentPostsCashForCrewOrPartner();
CrewCanCreateStandalonePayableAndInputInvoice();
PartnerCanCreateStandalonePayableAndInputInvoice();
PostingTwiceIsIdempotent();
```

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinancePostingServiceTests'
```

- [ ] **Step 3: Implement posting API**

```csharp
public interface IFinancePostingService
{
    Task<Guid> UpsertProjectQuantityReceivableAsync(CentralLedgerActor actor, Guid lineItemId, CancellationToken token);
    Task<Guid> CreateCrewPayableAsync(CentralLedgerActor actor, CreateCrewPayableRequest request, CancellationToken token);
    Task<Guid> CreatePartnerPayableAsync(CentralLedgerActor actor, CreatePartnerPayableRequest request, CancellationToken token);
}
```

Project quantity uses `LedgerSourceType.ProjectQuantity` plus `ContractLineItem.Id` as the unique source. Confirmed quantity sets `Final`; otherwise use estimated quantity × estimated unit price as `Provisional`. Existing project lines are not bulk-posted until the legacy duplicate-conflict report in Task 10 is reviewed.

- [ ] **Step 4: Add entry controls without duplicating storage**

Project pages retain receivable, collection, payment, and output-invoice sections. Crew and partner pages add payable, input-invoice, payment, and deduction controls. Every handler calls central services; no page writes EF entities directly.

- [ ] **Step 5: Run posting and page tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinancePostingServiceTests|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~PartnerStageResultAuthorizationTests'
```

Expected: multi-entry behavior and authorization tests pass.

### Task 9: Convert the uncommitted project workbook to central finance

**Files:**
- Modify: the four project-workbook infrastructure files.
- Modify: `src/EngineeringManager.Application/DataExchange/ProjectWorkbookDtos.cs` if central IDs or fields differ.
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookCentralLedgerTests.cs`
- Test: existing project-workbook tests.

- [ ] **Step 1: Write failing central-ledger workbook tests**

Assert exported finance sheets use central settlement/invoice/cash IDs, include settlement state, original amount, actual amount, base/current invoice amount, allocation status, and deduction option. Import must call central commands and remain all-or-nothing.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~ProjectWorkbookCentralLedgerTests'
```

- [ ] **Step 3: Replace direct old-table access**

Remove direct creation or updates of `ReceivableEntry`, `PayableEntry`, `CollectionEntry`, `PaymentEntry`, and `InvoiceEntry` from `ProjectWorkbookImporter`. Use central command requests inside the existing import transaction and keep stable workbook business keys.

- [ ] **Step 4: Preserve current workbook behavior**

Keep project selection, sheets, ZIP attachments, permissions, checksums, preview, concurrency, and atomic confirmation behavior unchanged.

- [ ] **Step 5: Run all project-workbook and central-ledger tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~ProjectWorkbook|FullyQualifiedName~CentralLedger|FullyQualifiedName~SimpleXlsxWorkbook'
```

Expected: all selected tests pass.

### Task 10: Generate legacy migration and reconciliation service

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/LegacyFinanceMigrationService.cs`
- Create: EF migration `CentralLedgerMultiEntryFinance` under `src/EngineeringManager.Infrastructure/Data/Migrations/`.
- Test: `tests/EngineeringManager.Tests/Application/LegacyFinanceMigrationTests.cs`
- Modify: `scripts/verify-backup-restore.ps1`.

- [ ] **Step 1: Write failing migration mapping tests**

Seed old receivable, payable, collection, payment, deduction, invoice, refund, reversal, account transfer, and payroll-crew-linked records. Assert migration creates one central record per legacy header, stable `FinanceLegacyMap` rows, correct allocations, and identical project/company/partner totals.

Add project quantity lines whose current amount exactly or approximately matches an old receivable. Assert the preflight report classifies them as possible duplicates and blocks quantity backfill until an explicit resolution exists.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~LegacyFinanceMigrationTests'
```

- [ ] **Step 3: Implement deterministic migration mapping**

Map old records as follows:

```text
ReceivableEntry -> FinanceSettlement (Receivable)
PayableEntry -> FinanceSettlement (Payable)
DeductionEntry -> FinanceDeduction
InvoiceEntry -> FinanceInvoice
InvoiceReceivableLink/InvoiceLineItemLink -> FinanceInvoiceAllocation
CollectionEntry -> FinanceCashEntry (Collection) + allocation when parent exists
PaymentEntry -> FinanceCashEntry (Payment) + allocation when parent exists
RefundOrReversalEntry/PaymentReversalEntry -> signed cash adjustment/reversal record
AccountTransfer -> FinanceCashEntry (InternalTransfer)
```

Records with missing relationships remain unallocated and appear in a migration exception report; do not guess a project or partner.

Before project-quantity backfill, write `artifacts/central-ledger-migration/preflight-conflicts.json` with:

```text
contract_line_item_id
project_id
contract_id
quantity_current_amount
candidate_legacy_receivable_ids
candidate_amounts
match_reason (exact-source, exact-amount, approximate-amount, no-candidate)
resolution (map-existing, create-source-settlement, manual-review)
```

The migration must reconcile legacy-only central totals exactly before adding any new quantity-source settlements. Quantity rows with `manual-review` resolution stop database application. Resolved `map-existing` rows attach the project-quantity source to the migrated settlement; `create-source-settlement` rows create one new source-linked settlement and report the intentional post-migration increase separately.

- [ ] **Step 4: Generate the migration but do not apply it**

```powershell
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = (Join-Path (Get-Location) '.dotnet')
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add CentralLedgerMultiEntryFinance --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
```

Expected: migration contains only the new central-ledger tables/indexes/constraints and deterministic data migration; it does not drop old finance tables.

- [ ] **Step 5: Inspect migration and verify no pending model changes**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\dotnet-tools\dotnet-ef.exe migrations has-pending-model-changes --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --no-build
git diff --check
```

Expected: EF reports no model changes after the generated migration; diff check passes.

## Phase 3 — Finance years, internal/external workbench, and permissions

### Task 11: Implement independent finance business years

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/FinanceBusinessYearService.cs`
- Create: `src/EngineeringManager.Web/Pages/Ledger/Years/Index.cshtml` and `.cs`.
- Test: `tests/EngineeringManager.Tests/Application/FinanceBusinessYearServiceTests.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`.

- [ ] **Step 1: Write failing year tests**

Cover custom non-overlapping ranges, automatic finance-date assignment, external/internal shared finance years, employee-year independence, project-lifetime independence, dynamic prior-year carry forward, and historical correction recalculation.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinanceBusinessYearServiceTests'
```

- [ ] **Step 3: Implement service**

```csharp
public interface IFinanceBusinessYearService
{
    Task<IReadOnlyList<FinanceBusinessYearDto>> ListAsync(CancellationToken token);
    Task<FinanceBusinessYearDto> CreateAsync(CentralLedgerActor actor, CreateFinanceBusinessYearRequest request, CancellationToken token);
    Task<FinanceBusinessYearDto?> ResolveAsync(DateOnly businessDate, CancellationToken token);
    Task DeleteAsync(CentralLedgerActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token);
}
```

Reject overlap with `existing.StartDate <= request.EndDate && existing.EndDate >= request.StartDate`.

- [ ] **Step 4: Add admin page and service registration**

The page lists name, start, end, current status, and record counts. It must explicitly label the feature “财务业务年度（仅中央账本）” and never reuse `IBusinessYearService`.

- [ ] **Step 5: Run finance-year and employee-year tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinanceBusinessYearServiceTests|FullyQualifiedName~BusinessYearServiceTests|FullyQualifiedName~EmployeeAnnualLedger'
```

Expected: both independent year systems pass.

### Task 12: Add permissions and collapsible Central Ledger navigation

**Files:**
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/EngineeringManager.Web/wwwroot/js/components/collapsible-nav.js`
- Modify: `src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `tests/EngineeringManager.Tests/Web/CentralLedgerAuthorizationTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`

- [ ] **Step 1: Write failing permission/navigation tests**

Assert external and internal ledger permissions are independent, query-only users cannot mutate, finance-year and reconciliation operations require dedicated permission, and the layout renders one collapsible parent with exactly two second-level links.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerAuthorizationTests|FullyQualifiedName~CentralLedgerPageTests'
```

- [ ] **Step 3: Add permission keys and role defaults**

```csharp
public const string ExternalLedgerRead = "ledger.external.read";
public const string ExternalLedgerManage = "ledger.external.manage";
public const string InternalLedgerRead = "ledger.internal.read";
public const string InternalLedgerManage = "ledger.internal.manage";
public const string FinanceYearsManage = "ledger.years.manage";
public const string FinanceReconciliationManage = "ledger.reconciliation.manage";
```

System and application administrators receive all. Finance receives external read/manage, internal read/manage, years, and reconciliation. Query-only receives external/internal read only. Project managers do not receive central-ledger access automatically.

- [ ] **Step 4: Implement collapsible menu**

Render a `<details>` navigation group containing `/Ledger/External` and `/Ledger/Internal`. JavaScript stores the open state in `localStorage` under `central-ledger-nav-open`; active child routes force the group open.

- [ ] **Step 5: Run authorization and asset tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerAuthorizationTests|FullyQualifiedName~CentralLedgerPageTests|FullyQualifiedName~PermissionCatalogTests|FullyQualifiedName~OfflineAssetsTests'
```

Expected: all selected tests pass.

### Task 13: Build external and internal ledger workbenches

**Files:**
- Create: external/internal page files and shared ledger partials.
- Create: `src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml` and `.cs`.
- Modify: CSS files and `Program.cs`.
- Modify: legacy `Pages/Finance/*` routes.
- Test: central-ledger page and authorization tests.

- [ ] **Step 1: Write failing rendered-page tests**

Assert the external page contains metrics and tabs for overview, receivables, payables, collections, payments, output invoices, input invoices, deductions, unallocated, anomalies, annual ledger, reconciliations, and modification logs. Assert internal page never renders external partners and uses own-company pairs.

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerPageTests'
```

- [ ] **Step 3: Implement shared filters and metrics**

Filters bind finance year, custom dates, legal entity, project, contract, crew, partner, settlement state, settlement/invoice/allocation status, anomaly flags, search, sorting, page, page size, and saved view. The metrics partial renders every specification metric, including “超前发票收款/付款” and separate “超结算收款/付款”.

- [ ] **Step 4: Implement unified entry/edit page**

The page switches fields by record type but posts to central commands. Deduction entry includes a checked/unchecked “同时扣减应开票金额”. Delete requires a confirmation dialog and mandatory reason. Cross-project allocations use repeatable rows with total validation.

- [ ] **Step 5: Preserve legacy routes**

`/Finance` redirects to `/Ledger/External`; `/Finance/Entries/Create` redirects to `/Ledger/Entries/Edit` with equivalent query parameters; `/Finance/Accounts` remains available for account management.

- [ ] **Step 6: Run page, authorization, workbench, and responsive tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedgerPageTests|FullyQualifiedName~CentralLedgerAuthorizationTests|FullyQualifiedName~ModuleDataWorkbenchTests|FullyQualifiedName~DataWorkbenchAssetTests'
```

Expected: all selected tests pass.

## Phase 4 — Reconciliation, database application, and delivery

### Task 14: Implement editable reconciliation snapshots

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Finance/FinanceReconciliationService.cs`
- Create: reconciliation pages.
- Test: `tests/EngineeringManager.Tests/Application/FinanceReconciliationServiceTests.cs`
- Test: central-ledger page tests.

- [ ] **Step 1: Write failing snapshot tests**

```csharp
SnapshotStoresStructuredMetricsBySettlement();
SnapshotDoesNotLockHistoricalRecords();
HistoricalEditShowsCurrentValueAndDifference();
PhysicalDeleteAppearsInSnapshotDifferenceLog();
NewSnapshotCreatesNewVersionWithoutOverwritingOldSnapshot();
UnauthorizedActorCannotCreateOrDeleteSnapshot();
```

- [ ] **Step 2: Verify RED**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinanceReconciliationServiceTests'
```

- [ ] **Step 3: Implement snapshot and comparison**

Snapshot creation stores query JSON, aggregate metric JSON, and one structured line per settlement/party/project combination. Details recompute current values using the same query service and compare by stable settlement ID; deleted items use `FinanceDeletionLog` snapshots.

The page and service must label this rule explicitly as “对账不锁定历史；修改后显示快照差异”.

- [ ] **Step 4: Implement reconciliation pages**

Index filters by finance year, date, legal entity, counterparty, scope, and version. Details display snapshot, current, difference, and links to audit/deletion logs. Do not render a lock or approval action.

- [ ] **Step 5: Run reconciliation and audit tests**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~FinanceReconciliationServiceTests|FullyQualifiedName~CentralLedgerCommandServiceTests|FullyQualifiedName~CentralLedgerPageTests'
```

Expected: all selected tests pass.

### Task 15: Back up and apply migration to EngineeringManager_Test

**Files:**
- Modify only as verification reveals within central-ledger/migration files.
- Produce: `artifacts/central-ledger-migration/verification-report.json`.

- [ ] **Step 1: Run pre-migration quality checks**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release
& .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release --no-build
& .\.tools\dotnet-tools\dotnet-ef.exe migrations has-pending-model-changes --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --no-build
```

Expected: build and tests pass; EF reports no pending changes.

- [ ] **Step 2: Create and verify a fresh test-database backup**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\verify-backup-restore.ps1
```

Expected: `BACKUP_RESTORE=PASS`, a `.bak` path, an attachment ZIP path, and deletion of the disposable restore database.

- [ ] **Step 3: Stop and obtain explicit user confirmation**

Report the backup file, test results, migration name, table list, and expected data mapping. Do not execute `database update` until the user confirms.

- [ ] **Step 4: Apply only to EngineeringManager_Test after confirmation**

```powershell
$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& .\.tools\dotnet-tools\dotnet-ef.exe database update --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj
```

Expected: the new migration is recorded in `__EFMigrationsHistory`; no production database is contacted.

- [ ] **Step 5: Run SQL reconciliation queries**

Generate JSON containing old/new counts and sums by project, legal entity, business partner, direction, invoices, cash, deductions, unallocated amount, and migration exceptions. Fail verification on any unexplained difference.

- [ ] **Step 6: Verify administrator and health state**

Read-only SQL checks must confirm `taozhiming` still exists with `SystemAdministrator`. Start the Release app and verify `/health/live` and `/health/ready` return 200.

### Task 16: Final regression, performance, browser acceptance, and documentation

**Files:**
- Modify: `docs/开发进度.md`, `README.md`, `docs/release-checklist.md`.
- Modify implementation/test files only if verification exposes a defect.

- [ ] **Step 1: Run focused central-ledger suite**

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CentralLedger|FullyQualifiedName~FinancePosting|FullyQualifiedName~FinanceBusinessYear|FullyQualifiedName~FinanceReconciliation|FullyQualifiedName~LegacyFinanceMigration|FullyQualifiedName~ProjectWorkbook'
```

Expected: zero failures.

- [ ] **Step 2: Run full quality gate**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

Expected: Release build 0 warnings/0 errors, all tests pass, `PUBLISH_RELEASE=PASS`, `QUALITY_GATE=PASS`.

- [ ] **Step 3: Run representative performance checks**

With representative test data, record external/internal overview, filtered detail, annual ledger, reconciliation details, and project workbook export timings. Targets: list/detail under 2 seconds, overview under 3 seconds, workbook export under 15 seconds.

- [ ] **Step 4: Run desktop browser acceptance**

At 1440px verify menu collapse persistence, external/internal isolation, every metric/tab/filter, project/crew/partner entry synchronization, provisional-to-final adjustment, deductions, cross-project allocations, physical delete logs, finance years, reconciliation differences, exports, and zero console/HTTP errors.

- [ ] **Step 5: Run agreed final mobile acceptance**

At 390px verify no page-level horizontal overflow; wide tables scroll only inside table containers; menus, filters, allocation rows, confirmation dialogs, and metrics remain usable.

- [ ] **Step 6: Update documentation only with observed results**

Record completed files, migration/backup paths, test counts, SQL reconciliation totals, browser results, performance timings, known limitations, production-migration boundary, and next step. Do not claim production deployment.

- [ ] **Step 7: Final working-tree review without Git mutation**

```powershell
$ErrorActionPreference = 'Stop'
git status --short
git diff --check
git diff --stat
```

Expected: no whitespace errors; all pre-existing project-workbook and `old-data/` state is preserved; no commit, branch, push, stash, or history rewrite occurred.
