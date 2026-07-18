# Temporary Workers Employee Merge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 将独立临时人员并入员工管理的 `Temporary` 特殊员工类型，迁移历史工资付款和项目归属，移除旧业务入口，并保持年度总账与历史链接连续。

**Architecture:** `Employee` 成为唯一人员主档，新增 `EmployeeType.Temporary`；工资发放统一使用 `PayrollPayment.EmployeeId`，不再使用临时人员专用外键。数据库迁移先建立 `PersonnelMigrationMap`，将已转正档案映射到既有员工、未关联档案生成特殊临时员工，再迁移工资付款，最后删除旧临时人员业务表和专用字段。旧 URL 通过迁移映射跳转到员工详情。

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQL Server migrations, SQLite application tests, xUnit, FluentAssertions.

---

## Scope And File Map

### Domain and application contracts

- Modify `src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`: add `EmployeeType.Temporary`; remove the `PayrollRecipientType.TemporaryWorker` branch after data migration.
- Modify `src/EngineeringManager.Domain/Employees/PayrollDisbursementRules.cs`: treat all direct people as employees and remove the temporary-recipient fields and `TemporaryAmount` category.
- Modify `src/EngineeringManager.Application/Payroll/PayrollDtos.cs`: remove `TemporaryWorkerId`, the temporary recipient DTO fields, and temporary-only overview totals.
- Modify `src/EngineeringManager.Application/Employees/EmployeeDtos.cs` only if a type-filter query value is needed; preserve existing constructor compatibility where possible.

### Persistence and migration

- Create `src/EngineeringManager.Infrastructure/Data/PersonnelMigrationMap.cs`: immutable legacy temporary-worker ID to employee ID mapping with migration timestamp.
- Modify `src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`: remove `TemporaryWorkerId` and `TemporaryWorker` navigation after the migration step.
- Modify `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`: register/configure `PersonnelMigrationMaps`, update the payroll recipient constraint, and remove the temporary-worker relationship.
- Create the next EF migration under `src/EngineeringManager.Infrastructure/Data/Migrations/` with explicit SQL data migration before dropping old columns/tables.
- Update `src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs` through EF tooling.

### Services, pages, and presentation

- Modify `src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`: resolve every direct person from `Employees`, use `employee:{id}` recipient keys, and remove temporary-worker queries/branches.
- Modify `src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`: seed a `Temporary` employee instead of a `TemporaryWorker` row.
- Modify `src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs` and `ExportService.cs`: accept and emit `特殊临时人员`/`Temporary`.
- Modify `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs` and `Edit.cshtml`: use one employee line collection with employee type labels; remove the temporary-person section.
- Modify `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`: remove the obsolete temporary-amount column and label; employee totals include special temporary employees.
- Modify `src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs` and `Index.cshtml`: support type filtering and show the third employee count.
- Modify `src/EngineeringManager.Web/Pages/Employees/Create.cshtml` if the enum option labels need explicit display names.
- Modify `src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs`: render `Temporary` as `特殊临时人员`.
- Modify `src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml.cs` and `Index.cshtml`: redirect the legacy list URL to employee filtering.
- Modify `src/EngineeringManager.Web/Pages/TemporaryWorkers/Details.cshtml.cs` and `Details.cshtml`: resolve `PersonnelMigrationMap` and redirect old detail URLs to employee details.
- Modify `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml` and `wwwroot/service-worker.js`: remove the old navigation item and old sensitive-path entry.
- Modify `src/EngineeringManager.Web/Program.cs`: remove the obsolete temporary-worker service registration.
- Modify `src/EngineeringManager.Domain/Security/PermissionKeys.cs`: move access to employee permissions and remove old temporary-worker permission defaults after compatibility checks.

### Tests

- Modify `tests/EngineeringManager.Tests/Domain/PayrollDisbursementRulesTests.cs` for unified employee recipients.
- Modify `tests/EngineeringManager.Tests/Application/PayrollDisbursementServiceTests.cs` and `Infrastructure/PayrollDisbursementModelTests.cs` to use a `Temporary` employee and assert employee totals.
- Create `tests/EngineeringManager.Tests/Application/PersonnelMigrationMapTests.cs` for mapping and redirect data behavior that is testable without SQL Server.
- Modify `tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`, `TemporaryWorkerPageTests.cs`, `OfflineAssetsTests.cs`, `PermissionCatalogTests.cs`, and `DevelopmentSampleDataSeederTests.cs` for the new navigation and type.
- Remove or replace `tests/EngineeringManager.Tests/Application/TemporaryWorkerServiceTests.cs` after the independent service is removed.

## Task 1: Add The Special Employee Type

**Files:** `src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`, `src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs`, `src/EngineeringManager.Web/Pages/Employees/Index.cshtml`, `tests/EngineeringManager.Tests/Domain/EmployeeTypeTests.cs`.

- [x] **Step 1: Write the failing type-label test.**

```csharp
[Fact]
public void TemporaryEmployeeTypeHasStableValueAndChineseLabel()
{
    ((int)EmployeeType.Temporary).Should().Be(3);
    EmployeeType.Temporary.ToChinese().Should().Be("特殊临时人员");
}
```

- [x] **Step 2: Run the focused test and verify it fails.**

Run from `EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter FullyQualifiedName~EmployeeTypeTests
```

Expected: FAIL because `EmployeeType.Temporary` and its display mapping do not exist.

- [x] **Step 3: Add the enum value and display mapping.**

Add `Temporary = 3` without changing the existing values. Extend the existing display switch with:

```csharp
EmployeeType.Temporary => "特殊临时人员",
```

Keep `Formal = 1` and `Labor = 2` unchanged for persisted data compatibility.

- [x] **Step 4: Update employee overview labels and run the test.**

Add a third overview item for `Model.Employees.Count(item => item.EmployeeType == EmployeeType.Temporary)`, and ensure the row uses `ToChinese()` for the type. Re-run the focused test; expected: PASS.

## Task 2: Make Payroll Direct-Person Lines Employee-Only

**Files:** `src/EngineeringManager.Domain/Employees/PayrollDisbursementRules.cs`, `src/EngineeringManager.Application/Payroll/PayrollDtos.cs`, `src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`, `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs`, `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml`, `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`.

- [x] **Step 1: Update tests to represent a special temporary employee.**

Replace temporary-recipient fixtures with an `Employee { EmployeeType = EmployeeType.Temporary }`. The request line must be:

```csharp
new PayrollDisbursementLineRequest(null, PayrollRecipientType.Employee, temporaryEmployee.Id, null, null, null, 3_000m, "临时人工")
```

Change assertions so the 3,000 amount is part of `EmployeeAmount`, and assert that no temporary-recipient value is present in the response.

- [x] **Step 2: Run the payroll domain and service tests and verify the expected compile/test failures.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter FullyQualifiedName~PayrollDisbursement
```

Expected: FAIL to compile because the tests and implementation still reference `TemporaryWorkerId` and `TemporaryAmount`.

- [x] **Step 3: Remove the temporary recipient branch from the domain rule.**

Keep `PayrollRecipientType.Employee` and `CrewWorker`. Remove `TemporaryWorkerId`, `ForTemporaryWorker`, the `TemporaryAmount` field, temporary key generation, and temporary validation. The summary becomes:

```csharp
public sealed record PayrollDisbursementSummary(
    decimal EmployeeAmount,
    decimal CrewAmount,
    decimal DetailAmount,
    decimal ActualAmount,
    decimal Difference,
    IReadOnlyList<PayrollCrewAmount> CrewAmounts);
```

`EmployeeAmount` includes all `Employee` rows regardless of `EmployeeType`.

- [x] **Step 4: Remove obsolete DTO fields.**

Remove `TemporaryWorkerId` from `PayrollDisbursementLineRequest` and `PayrollDisbursementLineDto`; remove `TemporaryAmount` from `PayrollDisbursementOverviewDto`. Preserve parameter order for all remaining fields and update every constructor call in tests and services.

- [x] **Step 5: Resolve direct people only from Employees.**

In `PayrollService.ResolveDisbursementLinesAsync`, remove the `temporaryIds` query and the temporary switch branch. For `PayrollRecipientType.Employee`, load any active employee, including `EmployeeType.Temporary`, and create:

```csharp
new ResolvedDisbursementLine(
    input,
    $"employee:{employee.Id:N}",
    employee.Name,
    employee.IdentityNumber,
    employee.Phone,
    employee.BankAccountNumber,
    employee.PositionTitle,
    null)
```

Update `ApplyDisbursementLines` and `GetDisbursementBatchAsync` to use only `EmployeeId` for direct people.

- [x] **Step 6: Replace the payroll editor’s three-person-list shape with employees plus crew workers.**

Load all active employees into `Input.EmployeeLines`; keep `CrewLines` for crew workers. Delete `TemporaryLines` and the separate temporary section. The employee label must include the employee number, name, and `ToChinese()` type so special temporary people remain distinguishable.

- [x] **Step 7: Run focused payroll tests and verify they pass.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PayrollDisbursementRulesTests|FullyQualifiedName~PayrollDisbursementServiceTests|FullyQualifiedName~PayrollDisbursementModelTests"
```

Expected: PASS, with special temporary employees included in employee totals and no temporary-recipient branch compiled.

## Task 3: Add The Legacy-ID Mapping Entity

**Files:** create `src/EngineeringManager.Infrastructure/Data/PersonnelMigrationMap.cs`; modify `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`; create `tests/EngineeringManager.Tests/Application/PersonnelMigrationMapTests.cs`.

- [x] **Step 1: Write the mapping model test.**

```csharp
[Fact]
public async Task MigrationMapKeepsLegacyIdAndNewEmployeeIdUnique()
{
    await using var fixture = await PersonnelMigrationFixture.CreateAsync();
    var legacyId = Guid.NewGuid();
    fixture.Db.PersonnelMigrationMaps.Add(new PersonnelMigrationMap
    {
        LegacyTemporaryWorkerId = legacyId,
        EmployeeId = fixture.Employee.Id,
        MigratedAt = DateTimeOffset.UtcNow
    });

    await fixture.Db.SaveChangesAsync();

    var map = await fixture.Db.PersonnelMigrationMaps.SingleAsync();
    map.LegacyTemporaryWorkerId.Should().Be(legacyId);
    map.EmployeeId.Should().Be(fixture.Employee.Id);
}
```

- [x] **Step 2: Run the test and verify it fails.**

Expected: FAIL because `PersonnelMigrationMap` and `DbSet` do not exist.

- [x] **Step 3: Implement the mapping entity and model configuration.**

Use these fields:

```csharp
public sealed class PersonnelMigrationMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegacyTemporaryWorkerId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTimeOffset MigratedAt { get; set; } = DateTimeOffset.UtcNow;
    public Employee Employee { get; set; } = null!;
}
```

Configure a unique index on `LegacyTemporaryWorkerId`, an index on `EmployeeId`, and a restricted foreign key to `Employees`. Do not add a foreign key to the table that the migration will drop.

- [x] **Step 4: Run the mapping test and verify it passes.**

Expected: PASS against the existing SQLite model.

## Task 4: Add The Data Migration With Preflight Guards

**Files:** `src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`, `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`, generated migration and snapshot under `src/EngineeringManager.Infrastructure/Data/Migrations/`.

- [x] **Step 1: Add a migration preflight test fixture for conflict rules.**

Add tests that represent these SQL preconditions: an unconverted temporary worker with an identity number already used by an employee must be rejected; a converted worker must map to the existing employee; an unconverted worker must receive a new employee ID and `EmployeeType.Temporary`.

- [x] **Step 2: Run the migration preflight tests and verify they fail.**

Expected: FAIL because the migration script and mapping behavior do not exist.

- [x] **Step 3: Generate the EF migration after model changes.**

From `EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = (Join-Path (Get-Location) '.dotnet')
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add MergeTemporaryWorkersIntoEmployees --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
```

- [x] **Step 4: Insert the explicit SQL data migration before destructive operations.**

The migration `Up` must perform these operations in order:

1. Create `PersonnelMigrationMaps`.
2. Abort with a clear SQL error if an unconverted temporary worker’s non-null identity number conflicts with an existing employee identity number, or if a generated `TMP-<legacy-guid>` employee number already exists.
3. Insert maps for `ConvertedEmployeeId` rows.
4. Insert new `Employee` rows for unconverted rows using `EmployeeType = 3`, `EmployeeNumber = 'TMP-' + GUID-without-dashes`, copied personal fields, `PositionTitle = Trade`, and `IsActive`/timestamps from the legacy row.
5. Insert corresponding map rows and primary project affiliations using the legacy created date and default project.
6. Fill only null personal fields on converted employees; never overwrite an existing non-null value.
7. Update every `PayrollPayment` referencing a legacy temporary worker to `RecipientType = 1`, mapped `EmployeeId`, `RecipientKey = 'employee:' + employee GUID`, and `TemporaryWorkerId = NULL`.
8. Assert that no payroll payment remains with a temporary worker reference.
9. Drop the old payroll foreign key, index, check constraint branch, and `TemporaryWorkerId` column.
10. Drop the `TemporaryWorkers` table after all references are gone.

Use `THROW` for preflight failures so the migration is atomic and leaves the old schema/data intact. The `PersonnelMigrationMaps` table remains after the old table is removed.

- [x] **Step 5: Update the EF model after the migration shape is generated.**

Remove `TemporaryWorkerId` and `TemporaryWorker` from `PayrollPayment`; update the check constraint to allow only `Employee` and `CrewWorker` direct recipients; ensure `PersonnelMigrationMaps` is represented in the model snapshot.

- [x] **Step 6: Verify the migration model and tests.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PersonnelMigration|FullyQualifiedName~PayrollModel"
```

Expected: PASS. The SQL Server migration itself must be applied to `EngineeringManager_Test` in the later verification checkpoint, not to any production database.

## Task 5: Move Imports, Exports, Sample Data, Permissions, and Navigation

**Files:** `src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`, `ExportService.cs`, `src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`, `src/EngineeringManager.Domain/Security/PermissionKeys.cs`, `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`, `src/EngineeringManager.Web/wwwroot/service-worker.js`, `src/EngineeringManager.Web/Program.cs`, employee and payroll pages.

- [x] **Step 1: Add import/export tests for `特殊临时人员`.**

Extend the existing standard employee import test with a row whose type is `特殊临时人员` and assert that the imported entity has `EmployeeType.Temporary`. Assert that export emits the same Chinese label.

- [x] **Step 2: Run the focused import/export tests and verify they fail.**

Expected: FAIL because import parsing accepts only formal/labor and export serializes the raw enum name.

- [x] **Step 3: Implement type parsing and export labels.**

Accept both `特殊临时人员` and `Temporary`. Export the stable Chinese display label used by the employee page. Do not reinterpret existing values `正式员工` and `劳务员工`.

- [x] **Step 4: Replace sample temporary rows with special employees.**

Update the sample builder so all demonstration payroll payments use `Employee` with `EmployeeType.Temporary`, `RecipientType.Employee`, and `employee:<id>` keys. Do not create `TemporaryWorker` rows.

- [x] **Step 5: Move permissions and remove old navigation.**

Grant special-person access through existing employee and payroll permissions. Remove temporary-worker permission defaults, the old sidebar link, and the old service registration. Remove `/TemporaryWorkers` from sensitive offline prefixes because `/Employees` remains sensitive.

- [x] **Step 6: Run presentation and seeder tests.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~StandardImport|FullyQualifiedName~DevelopmentSampleDataSeeder|FullyQualifiedName~PermissionCatalog|FullyQualifiedName~PayrollDisbursementPage|FullyQualifiedName~OfflineAssets"
```

Expected: PASS with no independent temporary navigation or permission entry.

## Task 6: Preserve Legacy URLs Through Redirect Pages

**Files:** `src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml.cs`, `Index.cshtml`, `Details.cshtml.cs`, `Details.cshtml`, `tests/EngineeringManager.Tests/Web/TemporaryWorkerPageTests.cs`.

- [x] **Step 1: Replace the legacy page test.**

Assert that the legacy index page redirects to `/Employees` with a `Temporary` filter, and that the legacy details page queries `PersonnelMigrationMaps` and redirects to `/Employees/Details?id=<mapped employee>`. Assert that the old page no longer renders an independent temporary-person form.

- [x] **Step 2: Run the page test and verify it fails.**

Expected: FAIL because the current pages render and edit `TemporaryWorker` records.

- [x] **Step 3: Implement the redirects.**

Use a minimal Razor Page model with `ApplicationDbContext` only. The index `OnGet` returns `RedirectToPage("/Employees/Index", new { employeeType = EmployeeType.Temporary })`; the details handler loads the map by legacy ID and redirects to `/Employees/Details` or returns `NotFound` with a migration message. Keep the route files until old bookmarks have a valid target.

- [x] **Step 4: Run the page test and verify it passes.**

Expected: PASS.

## Task 7: Remove Obsolete Services and Update Existing Tests

**Files:** `src/EngineeringManager.Application/TemporaryWorkers/`, `src/EngineeringManager.Infrastructure/TemporaryWorkers/`, `tests/EngineeringManager.Tests/Application/TemporaryWorkerServiceTests.cs`, `tests/EngineeringManager.Tests/Infrastructure/PayrollDisbursementModelTests.cs`, `tests/EngineeringManager.Tests/Application/PayrollDisbursementServiceTests.cs`, and all remaining `rg` hits.

- [x] **Step 1: Search for forbidden legacy references.**

```powershell
$ErrorActionPreference = 'Stop'
rg -n 'TemporaryWorkerId|TemporaryWorker\b|TemporaryWorkers|PayrollRecipientType\.TemporaryWorker|TemporaryAmount|temporary-worker:' EngineeringManager/src EngineeringManager/tests -g '*.cs' -g '*.cshtml' -g '*.js'
```

Expected before cleanup: only the migration documentation, `PersonnelMigrationMap.LegacyTemporaryWorkerId`, and the two redirect pages may remain.

- [x] **Step 2: Remove obsolete runtime services and replace old tests.**

Delete the independent temporary-worker DTO/service implementation and replace service tests with migration-map, employee-type, and redirect tests. Update model fixtures to create `EmployeeType.Temporary` employees. Do not remove the migration-map field name because it is the explicit legacy compatibility key.

- [x] **Step 3: Run the full source-reference search again.**

Expected: no runtime references to the removed table, foreign key, recipient enum branch, or old service registration outside the redirect compatibility code and migration documentation.

## Task 8: Apply And Verify The SQL Server Migration

**Files:** `scripts/verify-temporary-worker-merge.ps1` (create), `docs/superpowers/plans/2026-07-18-temporary-workers-employee-merge.md` (this plan).

- [x] **Step 1: Add a test-database-only verification script.**

The script must require `-DatabaseName`, reject names that are not exactly `EngineeringManager_Test` or do not end in `_Test`, set `$ErrorActionPreference = 'Stop'`, and use UTF-8 for any report file. It must query before/after counts and sums for temporary workers, mapped employees, payroll payments, payment amounts, account transactions, and employee annual balances.

- [x] **Step 2: Apply all migrations to the test database.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\reset-test-database.ps1 -DatabaseName 'EngineeringManager_Test'
```

If the reset script is not appropriate for preserving a fixture, create a temporary backup first and run the new migration against a disposable `_Test` database only. Never target production.

- [x] **Step 3: Run the merge verification script.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\verify-temporary-worker-merge.ps1 -DatabaseName 'EngineeringManager_Test'
```

Expected: temporary business table/columns are absent; every migrated payment points to an employee; migrated payment count and amount match the pre-migration report; account transaction totals and annual ledger balances remain unchanged; the migration map count equals the number of old temporary workers.

- [x] **Step 4: Run focused, full, and quality-gate verification.**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

Expected: all tests pass and the quality gate exits successfully.

## Self-Review Checklist

- [x] `EmployeeType` persisted values remain compatible (`Formal=1`, `Labor=2`, `Temporary=3`).
- [x] Existing converted employees are reused; no duplicate employee is created.
- [x] Identity and employee-number conflicts fail before destructive schema changes.
- [x] All historical temporary payments keep their amount, batch, project, account, snapshots, and account transaction.
- [x] Special temporary employees appear in normal employee, payroll, and annual-ledger paths.
- [x] Old URLs resolve through `PersonnelMigrationMap`.
- [x] No runtime code still writes the removed temporary-worker relation.
- [x] The test database is the only database used for migration verification.
