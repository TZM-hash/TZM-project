# Employee Management Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild employee management as a project-style workspace where employee pages own payable entries, payroll batches own actual disbursements and allocations, and employee payment history is read-only and traceable to payroll and the central ledger.

**Architecture:** Extend the existing employee annual-ledger and unified payroll models instead of creating duplicate ledgers. Employee wage, expense and interest/dividend records remain the payable sources; unified payroll payments remain the actual-payment source; one batch-level account transaction remains the central-ledger cash source. Personal advances use a dedicated financial-account type with an employee owner, an automatic read-only employee payable, and an explicit repayment link.

**Tech Stack:** .NET 8, ASP.NET Core Razor Pages, Entity Framework Core, SQL Server migrations, xUnit, FluentAssertions, vanilla JavaScript and the existing CSS workbench system.

**Execution note:** Work in the current workspace because the user explicitly requested uninterrupted execution and the repository instructions prefer the shortest path. Do not create commits or change Git history. Preserve unrelated working-tree changes.

---

## File Map

### Domain and application contracts

- Modify `src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`: add employee wage-entry and disbursement classifications.
- Modify `src/EngineeringManager.Domain/Finance/FinanceEnums.cs`: add personal-advance account type and repayment transaction source.
- Modify `src/EngineeringManager.Application/EmployeeAnnualLedger/EmployeeAnnualLedgerDtos.cs`: expose wage classification, editable row data, attachments and source links.
- Modify `src/EngineeringManager.Application/EmployeeAnnualLedger/IEmployeeAnnualLedgerService.cs`: add list/update operations used by inline editing.
- Modify `src/EngineeringManager.Application/EmployeeLedger/EmployeeLedgerDtos.cs`: simplify expense input to final amount and expose editable expense/other-payable rows.
- Modify `src/EngineeringManager.Application/EmployeeLedger/IEmployeeLedgerService.cs`: add detail queries and update operations.
- Modify `src/EngineeringManager.Application/Payroll/PayrollDtos.cs`: add disbursement type, wage nature, labor company, optional project and personal-advance repayment fields.

### Persistence and services

- Modify `src/EngineeringManager.Infrastructure/Data/EmployeeWageEntry.cs`: persist display classification, attachment and personal-advance source.
- Modify `src/EngineeringManager.Infrastructure/Data/ExpenseRecord.cs`: retain legacy category/adjustment columns but treat `Amount` as the editable final amount.
- Modify `src/EngineeringManager.Infrastructure/Data/EmployeeOtherPayment.cs`: persist optional attachment.
- Modify `src/EngineeringManager.Infrastructure/Data/FinancialAccount.cs`: persist personal account owner and employee link.
- Modify `src/EngineeringManager.Infrastructure/Data/PayrollBatch.cs`: persist disbursement type and personal-advance repayment target.
- Modify `src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`: persist payment category, wage category, labor company and optional line project.
- Modify `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`: configure new columns, relations, checks and indexes.
- Modify `src/EngineeringManager.Infrastructure/EmployeeAnnualLedger/EmployeeAnnualLedgerService.cs`: classify/edit wage rows and expose payroll-derived payment detail.
- Modify `src/EngineeringManager.Infrastructure/EmployeeLedger/EmployeeLedgerService.cs`: simplify expense writes and expose/update expense and interest/dividend rows.
- Modify `src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`: validate classifications, keep one cash transaction per batch and synchronize personal-advance payable/repayment effects.
- Add EF migration and update `ApplicationDbContextModelSnapshot.cs` through the repository EF command.

### Web

- Modify `src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs` and `.cshtml`: project-style list workbench, summary, pagination and no list inline editor.
- Modify `src/EngineeringManager.Web/Pages/Employees/Details.cshtml.cs` and `.cshtml`: employee workspace, five tabs, payable inline editors, read-only payments and activity rail.
- Modify `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs` and `.cshtml`: edit disbursement classifications, personal funding and repayment links per batch/line.
- Modify `src/EngineeringManager.Web/Pages/Finance/Accounts.cshtml.cs` and `.cshtml`: create and summarize personal-advance accounts.
- Modify `src/EngineeringManager.Web/Presentation/EmployeeDisplayText.cs`: centralize Chinese labels for new enums.
- Modify `src/EngineeringManager.Web/wwwroot/css/pages.css`: employee workspace responsive layout using existing project tokens.
- Modify `src/EngineeringManager.Web/wwwroot/js/site.js`: filter employee sub-tabs and dependent payroll inputs.

### Tests

- Modify `tests/EngineeringManager.Tests/Domain/EmployeeAnnualLedgerCalculatorTests.cs`.
- Modify `tests/EngineeringManager.Tests/Application/EmployeeAnnualLedgerServiceTests.cs`.
- Modify `tests/EngineeringManager.Tests/Application/EmployeeAnnualLedgerAggregationTests.cs`.
- Modify `tests/EngineeringManager.Tests/Application/PayrollDisbursementServiceTests.cs`.
- Modify `tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs` only if personal-account account summaries require it.
- Modify `tests/EngineeringManager.Tests/Web/EmployeeIndexPageTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/EmployeeAnnualLedgerPageTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`.

## Task 1: Domain Classifications and Display Contracts

- [ ] Add failing domain and web text tests for the six employee wage tabs, three payment tabs, social/migrant wage labels and personal-advance account label.
- [ ] Run:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 test --no-restore --filter "FullyQualifiedName~EmployeeAnnualLedgerCalculatorTests|FullyQualifiedName~EmployeeAnnualLedgerPageTests"
  ```

  Expected: failures because `EmployeeWageEntryType`, `PayrollDisbursementType`, `PayrollPaymentCategory` and display labels do not exist.

- [ ] Add these enums:

  ```csharp
  public enum EmployeeWageEntryType { Attendance = 1, Overtime = 2, Bonus = 3, Penalty = 4, Other = 5 }
  public enum PayrollDisbursementType { Wage = 1, Other = 2 }
  public enum PayrollPaymentCategory { Wage = 1, Other = 2 }
  public enum PayrollFundingSource { CompanyAccount = 1, PersonalAdvance = 2 }
  ```

- [ ] Add a personal-advance member to `FinancialAccountType` and a personal-advance repayment member to `AccountTransactionSourceType` without renumbering existing members.
- [ ] Add `EmployeeDisplayText` mappings and rerun the focused tests until green.

## Task 2: Persistence Model and Migration

- [ ] Add failing SQLite model tests proving that employee wage classification/attachment/source, personal account ownership, payroll batch funding/repayment and payroll line dimensions round-trip.
- [ ] Run the focused infrastructure tests and verify they fail on missing members.
- [ ] Add persistence fields:

  ```csharp
  EmployeeWageEntry.EntryType
  EmployeeWageEntry.AttachmentId
  EmployeeWageEntry.SourcePersonalAdvanceBatchId
  EmployeeWageEntry.IsSystemGenerated

  EmployeeOtherPayment.AttachmentId

  FinancialAccount.OwnerEmployeeId
  FinancialAccount.OwnerName

  PayrollBatch.DisbursementType
  PayrollBatch.FundingSource
  PayrollBatch.RepaysPersonalAdvanceAccountId

  PayrollPayment.PaymentCategory
  PayrollPayment.WageCategory
  PayrollPayment.LaborBusinessPartnerId
  PayrollPayment.ProjectId
  ```

- [ ] Configure optional relationships with `Restrict` deletes. Preserve the existing payroll recipient constraint and add indexes for employee/year/type, personal account owner, payroll employee/category and personal-advance source batch.
- [ ] Generate migration:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 ef migrations add EmployeeManagementWorkspace --project src\EngineeringManager.Infrastructure --startup-project src\EngineeringManager.Web
  ```

- [ ] Inspect the migration to ensure legacy rows default to attendance wage, company-account funding and wage payment category; no table or column is dropped.
- [ ] Rerun focused model tests until green.

## Task 3: Employee Payable Services

- [ ] Add failing application tests for:

  - penalty entries accepting a positive input and storing a negative final amount;
  - wage rows returning entry type, company/project/crew labels and attachment metadata;
  - direct editing of final expense amount without category or adjustment math;
  - interest/dividend/other rows returning their original entry type;
  - batch update rejecting stale concurrency stamps;
  - system-generated personal-advance wage rows rejecting direct edits.

- [ ] Extend request/DTO contracts with explicit row identifiers, concurrency stamps, attachment uploads and display labels.
- [ ] Implement list and update operations in the annual-ledger and employee-ledger services. Keep old service methods compatible where existing tests and imports still use them.
- [ ] Store an uploaded attachment through the existing `Attachment` entity and link it to the payable row. Replacing an attachment creates the new record before changing the foreign key; historical attachment deletion remains out of scope.
- [ ] Write audit logs for row updates with before/after JSON and the supplied reason.
- [ ] Run:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 test --no-restore --filter "FullyQualifiedName~EmployeeAnnualLedgerServiceTests|FullyQualifiedName~EmployeeAnnualLedgerAggregationTests"
  ```

  Expected: all employee payable service tests pass.

## Task 4: Payroll Source of Truth and Personal Advances

- [ ] Add failing payroll service tests proving:

  - a batch still creates exactly one cash transaction for its actual total;
  - employee lines retain wage/other category, social/migrant wage nature, labor company and optional project;
  - migrant wage requires a labor company but permits a null project;
  - a personal-advance account must have an owner and the selected account must be type `PersonalAdvance`;
  - a confirmed personally funded batch creates or updates one system-generated `Other` wage payable for the account owner;
  - that payable counts in employee balance but is marked excluded from wage-cost reporting;
  - editing or voiding the source batch updates or reverses the system payable;
  - a company-funded `Other` batch linked as repayment creates a balancing inflow on the personal-advance account and never creates a second employee payment allocation.

- [ ] Extend payroll DTOs and page inputs with the required batch and line dimensions.
- [ ] In `PayrollService`, validate dimensions before mutation, then synchronize batch, lines, central transaction, personal-advance account effect and automatic employee payable in the existing database transaction.
- [ ] Keep the source batch ID and payroll line ID in employee receipt DTOs for exact traceability.
- [ ] Run:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 test --no-restore --filter "FullyQualifiedName~PayrollDisbursementServiceTests|FullyQualifiedName~PayrollDisbursementFinanceTests"
  ```

  Expected: payroll and personal-advance tests pass with one cash transaction per disbursement batch.

## Task 5: Employee List Workbench

- [ ] Replace existing web tests that expect list inline editing with failing tests for project-style headings, annual summaries, default financial columns, optional column keys, pagination and `查看`/`详细编辑` actions.
- [ ] Extend `Employees/Index.cshtml.cs` to load the selected/current business year, annual ledger summaries, filters and an in-memory paged result while preserving sensitive-data masking.
- [ ] Replace the list view with:

  - heading actions for annual ledger, certificate overview and new employee;
  - eight-item employee/finance overview strip;
  - the shared data workbench;
  - configurable master, affiliation and financial columns;
  - no `data-inline-edit` list row or quick-edit post handler.

- [ ] Run:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 test --no-restore --filter "FullyQualifiedName~EmployeeIndexPageTests|FullyQualifiedName~InlineEditingPageTests"
  ```

  Expected: employee list/workbench tests pass.

## Task 6: Employee Detail Workspace

- [ ] Replace the six-tab page tests with failing assertions for exactly five main tabs, wage sub-tabs, read-only payment history, project-style inline editors, attachment controls and activity rail.
- [ ] Refactor `Details.cshtml.cs` into explicit load/query and post handlers:

  - employee quick edit;
  - create/update wage rows;
  - create/update expense rows;
  - create/update interest/dividend/other payable rows;
  - create/update certificate rows through the existing certificate service;
  - attachment preview handlers;
  - no receipt/payment create handler.

- [ ] Rebuild `Details.cshtml` with the confirmed top summary, base-detail quick editor and five tabs in this order:

  ```text
  工资明细 -> 报销明细 -> 利息分红 -> 付款记录 -> 证书管理
  ```

- [ ] Wage sub-tabs must be `全部、考勤工资、加班工资、奖金、罚款、其他`; system-generated personal-advance rows are read-only source links.
- [ ] Expense summary must contain only `报销总金额`; its default table columns are date, amount, project, receipt number, attachment and notes.
- [ ] Interest/dividend sub-tabs must be `全部、分红、利息、其他应付`.
- [ ] Payment sub-tabs must be `全部、工资、其他`; payment rows have no form controls and link to `/Payroll/Edit` with `lineId` and `returnUrl`.
- [ ] Add the right activity rail from audit/source data and retain service-side masking.
- [ ] Run employee page and authorization tests until green.

## Task 7: Payroll and Personal Account UI

- [ ] Add failing payroll page tests for disbursement type, funding source, personal account, repayment target, wage category, labor company and optional line project fields.
- [ ] Extend finance account creation so personal-advance accounts require owner name and optionally link an employee. Show accumulated advance, repaid and outstanding values without presenting the account as a company bank balance.
- [ ] Extend payroll editing so:

  - batch funding chooses company account or personal-advance account;
  - `Other` batches can select a personal account repayment target;
  - employee lines choose wage/other, social/migrant wage, labor company and optional project;
  - client-side controls hide irrelevant inputs but server validation remains authoritative.

- [ ] Preserve exact line highlighting and smart return behavior.
- [ ] Run payroll page, finance authorization and account tests until green.

## Task 8: Styling, Assets and Responsive Behavior

- [ ] Add failing asset/page tests for employee workspace classes, five stable tab panels, compact tables, non-overlapping mobile activity rail and source-highlight affordance.
- [ ] Add scoped employee styles to `pages.css`, reusing project workspace variables, 8px-or-less radii, stable table geometry and existing quick-edit controls.
- [ ] Add small behavior hooks in `site.js` for employee category filters and payroll dependent fields. Do not duplicate the generic quick-edit component.
- [ ] Run responsive and offline asset tests.

## Task 9: Migration, Full Verification and Manual UI Check

- [ ] Run formatting/build:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 build --no-restore
  ```

- [ ] Run targeted employee/payroll/finance tests, then the complete test suite because this change crosses shared ledger behavior:

  ```powershell
  $ErrorActionPreference = 'Stop'
  .\scripts\dotnet.ps1 test --no-restore
  ```

- [ ] Apply the migration to a disposable test database using the repository reset script and verify employee, payroll, account-transaction and payable counts before and after.
- [ ] Start the web app on an unused local port and inspect employee list/detail, payroll edit and finance accounts at desktop and mobile sizes. Verify no text overlap, all five tabs render, read-only payments contain no edit controls, and source links preserve return navigation.
- [ ] Run `git diff --check` and inspect `git status --short`; leave unrelated and generated test-result files untouched.
