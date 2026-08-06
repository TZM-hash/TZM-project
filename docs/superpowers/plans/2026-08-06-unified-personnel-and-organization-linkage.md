# Unified Personnel and Organization Linkage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Keep each checkbox as the execution ledger and stop only after the verification gate passes.

**Goal:** Upgrade employee management into a unified, date-effective personnel system with editable linked affiliations, department ownership, organization statistics, and exact project/personnel drill-downs for companies, construction crews, and business partners.

**Architecture:** Add one Person master record and one date-effective PersonnelEngagementHistory stream. Existing Employee and ConstructionWorker rows remain business records bridged by a unique PersonId; all public identity edits and affiliation changes go through IPersonnelService. A read-only IOrganizationSummaryService calculates the same project, personnel, and department metrics for all three organization kinds, while Razor Pages expose the filters and links.

**Tech Stack:** ASP.NET Core Razor Pages, .NET 10, EF Core SQL Server/SQLite tests, xUnit, FluentAssertions, vanilla ES-module page scripts, Playwright/browser verification.

---

### Task 1: Establish domain rules and red tests

**Files:**
- Create: EngineeringManager/src/EngineeringManager.Domain/Personnel/PersonnelEnums.cs
- Create: EngineeringManager/src/EngineeringManager.Domain/Personnel/PersonnelEngagementRules.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Domain/PersonnelEngagementRulesTests.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Application/PersonnelServiceTests.cs

- [ ] **Step 1: Write the failing rule tests**

Add tests that assert this exact API and behavior:

~~~
[Fact]
public void Overlapping_primary_periods_are_rejected()
{
    var periods = new[]
    {
        new EngagementPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), true),
        new EngagementPeriod(new DateOnly(2026, 3, 1), null, true)
    };

    var action = () => PersonnelEngagementRules.ValidatePrimaryPeriods(periods);

    action.Should().Throw<InvalidOperationException>().WithMessage("*重叠*");
}

[Fact]
public void Latest_effective_primary_affiliation_is_current()
{
    var current = PersonnelEngagementRules.SelectCurrent(
        new[]
        {
            new CurrentEngagement(new DateOnly(2026, 1, 1), null, true, "旧项目"),
            new CurrentEngagement(new DateOnly(2026, 6, 1), null, true, "新项目")
        }, new DateOnly(2026, 8, 6));

    current!.ProjectName.Should().Be("新项目");
}
~~~

The service tests must also fail before implementation for: one Person shared by an employee and a worker, an internal-to-external switch closing the old interval, and a later effective project becoming current.

- [ ] **Step 2: Run the focused tests and verify RED**

Run from D:\AI\TZM-project\EngineeringManager:

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PersonnelEngagementRulesTests|FullyQualifiedName~PersonnelServiceTests" --no-restore
~~~

Expected: compilation fails because PersonnelEngagementRules, EngagementPeriod, CurrentEngagement, and IPersonnelService do not yet exist.

- [ ] **Step 3: Implement the minimal pure rules**

Define PersonnelScope { Internal = 1, External = 2 }, ExternalPersonnelType { ConstructionCrew = 1, BusinessPartner = 2, Other = 3 }, and these records/methods:

~~~
public sealed record EngagementPeriod(DateOnly StartDate, DateOnly? EndDate, bool IsPrimary);
public sealed record CurrentEngagement(DateOnly StartDate, DateOnly? EndDate, bool IsPrimary, string? ProjectName);

public static void ValidatePrimaryPeriods(IEnumerable<EngagementPeriod> source)
{
    var periods = source.Where(item => item.IsPrimary).OrderBy(item => item.StartDate).ToArray();
    for (var index = 1; index < periods.Length; index++)
        if (periods[index - 1].EndDate is null || periods[index].StartDate <= periods[index - 1].EndDate)
            throw new InvalidOperationException("同一人员的主要身份归属时间区间不能重叠。");
}

public static CurrentEngagement? SelectCurrent(IEnumerable<CurrentEngagement> source, DateOnly asOf)
    => source.Where(item => item.IsPrimary && item.StartDate <= asOf && (item.EndDate is null || item.EndDate >= asOf))
        .OrderByDescending(item => item.StartDate).FirstOrDefault();
~~~

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the same dotnet test command; expected: all new rule tests pass.

- [ ] **Step 5: Commit the isolated domain slice**

~~~
$ErrorActionPreference = 'Stop'
git add .\src\EngineeringManager.Domain\Personnel .\tests\EngineeringManager.Tests\Domain\PersonnelEngagementRulesTests.cs .\tests\EngineeringManager.Tests\Application\PersonnelServiceTests.cs
git commit -m "test: define personnel engagement rules"
~~~

### Task 2: Add the unified persistence model and migration-safe bridge

**Files:**
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Data/Person.cs
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Data/PersonnelEngagementHistory.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Data/Employee.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Data/ConstructionWorker.cs
- Modify: EngineeringManager/src/EngineeringManager.Domain/Organization/OrganizationUnit.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/20260806090000_UnifiedPersonnelAndOrganizationOwnership.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/UnifiedPersonnelModelTests.cs

- [ ] **Step 1: Add persistence red tests**

Use an in-memory SQLite fixture with EnsureCreatedAsync and assert Person.IdentityNumberNormalized is unique when non-null, Employee.PersonId and ConstructionWorker.PersonId have unique indexes, one history row can reference a legal entity or a business partner but not both, and an owned department cannot have a parent owned by a different organization.

- [ ] **Step 2: Add the entities and DbContext mappings**

Person contains Id, PersonNumber, Name, IdentityNumber, IdentityNumberNormalized, Phone, BankAccountNumber, BankName, Notes, IsActive, CreatedAt, UpdatedAt, and ConcurrencyStamp; PersonnelEngagementHistory contains PersonId, Scope, nullable InternalType/ExternalType, LegalEntityId, BusinessPartnerId, OrganizationUnitId, ProjectId, CrewBusinessPartnerId, PositionTitle, StartDate, EndDate, IsPrimary, Notes, Reason, and ConcurrencyStamp.

Configure max lengths, concurrency tokens, filtered unique identity index, unique person-number index, bridge indexes, restrictive foreign keys, and a check constraint enforcing exactly one valid scope-specific subtype and at most one organization owner. Add DbSet<Person>, DbSet<PersonnelEngagementHistory>, and PersonId properties to the two legacy business records. Extend OrganizationUnit with LegalEntityId, BusinessPartnerId, and IsAuthorizationScope.

The organization code index changes from global uniqueness to HasIndex(unit => new { unit.LegalEntityId, unit.BusinessPartnerId, unit.Code }).IsUnique() with a filtered SQL Server predicate that permits system-level units.

- [ ] **Step 3: Write the additive migration and backfill SQL**

The migration adds nullable bridge columns first, creates People and PersonnelEngagementHistories, inserts one person per legacy employee/worker, copies normalized public fields, fills Employee.PersonId/ConstructionWorker.PersonId, converts existing employee affiliations and active crew memberships into engagement rows, and records ambiguous identity-number collisions in PersonnelMigrationMaps without merging. It then creates the filtered indexes and leaves legacy tables intact. The Down method drops only the new tables/columns and never deletes legacy business data.

- [ ] **Step 4: Run model and migration tests**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~UnifiedPersonnelModelTests" --no-restore
dotnet ef migrations script --project .\src\EngineeringManager.Infrastructure --startup-project .\src\EngineeringManager.Web --context ApplicationDbContext --idempotent --output .\artifacts\unified-personnel-migration.sql
~~~

Expected: PASS on SQLite model creation and all constraint assertions; the generated script is inspected and never applied to production.

- [ ] **Step 5: Commit persistence**

~~~
$ErrorActionPreference = 'Stop'
git add .\src\EngineeringManager.Domain\Organization\OrganizationUnit.cs .\src\EngineeringManager.Infrastructure\Data .\tests\EngineeringManager.Tests\Infrastructure\UnifiedPersonnelModelTests.cs
git commit -m "feat: add unified personnel persistence model"
~~~

### Task 3: Implement the unified personnel application contract and bridge services

**Files:**
- Create: EngineeringManager/src/EngineeringManager.Application/Personnel/PersonnelDtos.cs
- Create: EngineeringManager/src/EngineeringManager.Application/Personnel/IPersonnelService.cs
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Personnel/PersonnelService.cs
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Personnel/PersonPublicDataSynchronizer.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Employees/EmployeeService.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/ConstructionCrews/ConstructionCrewService.cs
- Modify: EngineeringManager/src/EngineeringManager.Web/Program.cs
- Modify: EngineeringManager/tests/EngineeringManager.Tests/Application/PersonnelServiceTests.cs

- [ ] **Step 1: Define the exact application records and failing service tests**

Expose PersonnelListQuery, PersonnelListItemDto, PersonnelDetailsDto, PersonnelAffiliationDto, PersonnelOptionSetDto, SavePersonRequest, SavePersonnelAffiliationRequest, and SwitchPersonnelScopeRequest. IPersonnelService must provide CreateAsync, GetAsync, ListAsync, SavePublicDataAsync, SaveAffiliationAsync, SwitchScopeAsync, GetOptionsAsync, and ResolvePersonIdForEmployeeAsync.

Add tests for public-data synchronization to both legacy records, filtered options, rejection of an external worker without a crew, transaction rollback on invalid project ownership, and optimistic-concurrency failure.

- [ ] **Step 2: Implement PersonPublicDataSynchronizer**

Normalize required names and optional strings, normalize identity numbers by removing spaces and hyphens, and update Person, the linked Employee, and the linked ConstructionWorker in the same tracked unit of work. Never use name, phone, or bank account as an automatic merge key.

- [ ] **Step 3: Implement current-affiliation and scope-switch commands**

SaveAffiliationAsync validates organization ownership, active project/crew role, date ordering, and overlap through PersonnelEngagementRules; it closes the current primary row at effectiveDate.AddDays(-1) and inserts a new row with the selected values. SwitchScopeAsync performs the same close/insert operation, creates or reactivates the corresponding business record when needed, creates a crew membership for an external construction-crew identity, and writes one audit record inside BeginTransactionAsync.

- [ ] **Step 4: Route legacy creates/updates through the synchronizer**

EmployeeService.CreateAsync creates a Person and stores its PersonId; UpdateAsync calls the synchronizer before saving. ConstructionCrewService.AddWorkerAsync creates a Person, sets PersonId, and TransferWorkerAsync updates the engagement history as well as the legacy membership. Existing callers and payroll foreign keys remain unchanged.

- [ ] **Step 5: Register and run service tests**

Register IPersonnelService and PersonPublicDataSynchronizer in Program.cs. Run:

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PersonnelServiceTests|FullyQualifiedName~EmployeeServiceTests|FullyQualifiedName~ConstructionCrewServiceTests" --no-restore
~~~

Expected: PASS, including old employee/crew regression tests.

- [ ] **Step 6: Commit the service slice**

~~~
$ErrorActionPreference = 'Stop'
git add .\src\EngineeringManager.Application\Personnel .\src\EngineeringManager.Infrastructure\Personnel .\src\EngineeringManager.Infrastructure\Employees\EmployeeService.cs .\src\EngineeringManager.Infrastructure\ConstructionCrews\ConstructionCrewService.cs .\src\EngineeringManager.Web\Program.cs .\tests\EngineeringManager.Tests\Application\PersonnelServiceTests.cs
git commit -m "feat: add unified personnel service"
~~~

### Task 4: Add personnel workbenches and compatibility routing

**Files:**
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/Internal/Index.cshtml.cs
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/Internal/Index.cshtml
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/External/Index.cshtml.cs
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/External/Index.cshtml
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/Details.cshtml.cs
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Personnel/Details.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Employees/_EmployeeSubNavigation.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml
- Create: EngineeringManager/tests/EngineeringManager.Tests/Web/PersonnelPageTests.cs

- [ ] **Step 1: Add page-model red tests**

Assert that the rendered navigation says “人员管理”, exposes /Personnel/Internal and /Personnel/External, retains /Employees, and that internal/external query parameters are passed to IPersonnelService.

- [ ] **Step 2: Implement internal and external list pages**

Both pages use a shared PersonnelListQuery and display current scope, organization, department, project, crew, active status, and a link to /Personnel/Details?personId=.... Internal rows retain links to employee ledger/certificates; external rows retain crew/partner links. Add Search, DepartmentId, LegalEntityId, BusinessPartnerId, subtype, and active filters as real query parameters.

- [ ] **Step 3: Implement unified details and legacy redirects**

Personnel/Details loads by PersonId, shows public data and engagement history, and links to the legacy employee/crew business pages. If a legacy /Employees/Details?id=... request is received, resolve Employee.PersonId and redirect to the unified details page while preserving tab and businessYearId; old list/bookmark URLs continue to render.

- [ ] **Step 4: Run page tests and commit**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PersonnelPageTests|FullyQualifiedName~EmployeeIndexPageTests" --no-restore
git add .\src\EngineeringManager.Web\Pages\Personnel .\src\EngineeringManager.Web\Pages\Shared\_Layout.cshtml .\src\EngineeringManager.Web\Pages\Employees .\tests\EngineeringManager.Tests\Web\PersonnelPageTests.cs
git commit -m "feat: add internal and external personnel workbenches"
~~~

Expected: PASS with the old employee page assertions unchanged except for the visible navigation label.

### Task 5: Make the four current-affiliation fields editable and linked

**Files:**
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml.cs
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml
- Create: EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/personnel-affiliation.js
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml.css (create if absent; otherwise the existing employee detail stylesheet)
- Create: EngineeringManager/tests/EngineeringManager.Tests/Web/PersonnelAffiliationPageTests.cs

- [ ] **Step 1: Write failing page tests**

Assert the yellow current-affiliation area contains four named selects, dependent option data attributes, an effective date and reason, and the form posts to SaveAffiliation; assert a selected company limits department/project values and an external crew forces the crew select.

- [ ] **Step 2: Add server-side option loading and command handler**

Load PersonnelOptionSetDto from IPersonnelService for the employee’s PersonId. Add AffiliationInput with LegalEntityId, BusinessPartnerId, OrganizationUnitId, ProjectId, CrewBusinessPartnerId, EffectiveDate, PositionTitle, Reason, and ConcurrencyStamp; OnPostSaveAffiliationAsync calls SaveAffiliationAsync and returns to the same tab. On validation/concurrency errors, reload options while preserving posted values.

- [ ] **Step 3: Replace read-only markup with linked controls**

Render company/unit, department, project, and crew selects with stable names. Each option carries data-legal-entity-id, data-business-partner-id, and data-role="crew"; the project list is filtered by server-provided organization links. Clearing a parent clears all dependent child values before submission. The effective date defaults to today and the old affiliation remains in the history table.

- [ ] **Step 4: Implement the ES module behavior**

personnel-affiliation.js listens for change on parent selects, toggles option visibility, clears invalid selections, sets the external unit as the crew when it has the construction-crew role, and never trusts hidden client values over server validation.

- [ ] **Step 5: Run focused page/asset tests and commit**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PersonnelAffiliationPageTests|FullyQualifiedName~InlineEditingPageTests" --no-restore
git add .\src\EngineeringManager.Web\Pages\Employees\Details.cshtml .\src\EngineeringManager.Web\Pages\Employees\Details.cshtml.cs .\src\EngineeringManager.Web\wwwroot\js\pages\personnel-affiliation.js .\tests\EngineeringManager.Tests\Web\PersonnelAffiliationPageTests.cs
git commit -m "feat: edit linked personnel affiliations"
~~~

Expected: PASS, with the four controls present and no asset fingerprint failures.

### Task 6: Implement department ownership and maintenance for all organizations

**Files:**
- Modify: EngineeringManager/src/EngineeringManager.Application/Organization/OrganizationDtos.cs
- Modify: EngineeringManager/src/EngineeringManager.Application/Organization/IOrganizationService.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Organization/OrganizationService.cs
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Organization/Departments.cshtml.cs
- Create: EngineeringManager/src/EngineeringManager.Web/Pages/Organization/Departments.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Details.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml
- Create: EngineeringManager/tests/EngineeringManager.Tests/Application/OrganizationDepartmentServiceTests.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Web/OrganizationDepartmentPageTests.cs

- [ ] **Step 1: Add failing ownership and CRUD tests**

Cover same-organization code uniqueness, cross-organization duplicate codes, parent ownership rejection, disabling a referenced department, and personnel-count drill-down URL generation.

- [ ] **Step 2: Extend the organization contract**

Add OrganizationOwnerKind, DepartmentDto, SaveDepartmentRequest, ListDepartmentsAsync, SaveDepartmentAsync, and DeactivateDepartmentAsync. The service accepts exactly one of LegalEntityId/BusinessPartnerId, checks parent ownership, and rejects physical deletion when a history row references the unit.

- [ ] **Step 3: Build the reusable department page**

Route /Organization/Departments?legalEntityId=... or /Organization/Departments?businessPartnerId=.... It displays active/inactive rows, add/edit/enable/disable forms, current-person count, and a link to /Personnel/Internal or /Personnel/External with the owner/dept query parameters. Add “部门设置” links to all company, crew, and partner details pages.

- [ ] **Step 4: Run tests and commit**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~OrganizationDepartment" --no-restore
git add .\src\EngineeringManager.Application\Organization .\src\EngineeringManager.Infrastructure\Organization .\src\EngineeringManager.Web\Pages\Organization .\src\EngineeringManager.Web\Pages\Companies\Details.cshtml .\src\EngineeringManager.Web\Pages\Partners\Details.cshtml .\src\EngineeringManager.Web\Pages\Crews\Details.cshtml .\tests\EngineeringManager.Tests\Application\OrganizationDepartmentServiceTests.cs .\tests\EngineeringManager.Tests\Web\OrganizationDepartmentPageTests.cs
git commit -m "feat: add organization department maintenance"
~~~

Expected: PASS for service validation and all three organization entry links.

### Task 7: Add one statistics service and exact project/personnel drill-downs

**Files:**
- Create: EngineeringManager/src/EngineeringManager.Application/Organization/OrganizationSummaryDtos.cs
- Create: EngineeringManager/src/EngineeringManager.Application/Organization/IOrganizationSummaryService.cs
- Create: EngineeringManager/src/EngineeringManager.Infrastructure/Organization/OrganizationSummaryService.cs
- Modify: EngineeringManager/src/EngineeringManager.Application/Companies/CompanyDtos.cs
- Modify: EngineeringManager/src/EngineeringManager.Application/Partners/PartnerDtos.cs
- Modify: EngineeringManager/src/EngineeringManager.Application/ConstructionCrews/ConstructionCrewDtos.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/ConstructionCrews/ConstructionCrewService.cs
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Details.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml
- Modify: EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml
- Create: EngineeringManager/tests/EngineeringManager.Tests/Application/OrganizationSummaryServiceTests.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Web/OrganizationSummaryLinkTests.cs

- [ ] **Step 1: Write failing aggregation tests**

Seed projects in every ProjectStage, duplicate partner/construction links, internal and external histories, and ended memberships. Assert de-duplicated stage counts, current-only personnel counts, and exact links containing LegalEntityId, BusinessPartnerId, Stages, Scope, subtype, and active filters.

- [ ] **Step 2: Implement the shared summary contract**

Use OrganizationSummaryQuery(OrganizationOwnerKind Kind, Guid Id, DateOnly AsOf) and return OrganizationSummaryDto(ProjectStats, PersonnelStats, DepartmentStats). Project stats use legal-entity links for companies, partner links for partners, and the union of partner links plus construction records for crews; all project IDs are Distinct() before stage grouping. Personnel stats use current primary engagement rows and current memberships only.

- [ ] **Step 3: Add exact project filtering**

Extend ProjectListQuery with Guid? BusinessPartnerId, ProjectListOptionsDto with partner options, and Projects/Index.cshtml.cs/.cs with bind/export/view state. In ProjectService.SearchProjectsAsync, apply:

~~~
if (query.BusinessPartnerId.HasValue)
{
    var partnerId = query.BusinessPartnerId.Value;
    projectQuery = projectQuery.Where(project =>
        project.Partners.Any(link => link.BusinessPartnerId == partnerId)
        || project.ConstructionRecords.Any(record => record.CrewBusinessPartnerId == partnerId));
}
~~~

Keep company, partner, and stage filters as SQL predicates and combine them by intersection.

- [ ] **Step 4: Render statistic cards and drill-down links**

Add cards for total, in-progress, suspended, completed-unsettled, partially settled, settled-archived, total/current personnel, subtypes, and departments. Each card is an anchor to the exact filtered page; permissions are rechecked by the destination page.

- [ ] **Step 5: Run aggregation and project tests and commit**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~OrganizationSummaryServiceTests|FullyQualifiedName~OrganizationSummaryLinkTests|FullyQualifiedName~ProjectServiceTests" --no-restore
git add .\src\EngineeringManager.Application\Organization .\src\EngineeringManager.Application\Companies\CompanyDtos.cs .\src\EngineeringManager.Application\Partners\PartnerDtos.cs .\src\EngineeringManager.Application\ConstructionCrews\ConstructionCrewDtos.cs .\src\EngineeringManager.Infrastructure\Organization\OrganizationSummaryService.cs .\src\EngineeringManager.Infrastructure\Companies\CompanyManagementService.cs .\src\EngineeringManager.Infrastructure\Partners\BusinessPartnerService.cs .\src\EngineeringManager.Infrastructure\ConstructionCrews\ConstructionCrewService.cs .\src\EngineeringManager.Application\Projects\ProjectDtos.cs .\src\EngineeringManager.Infrastructure\Projects\ProjectService.cs .\src\EngineeringManager.Web\Pages\Companies .\src\EngineeringManager.Web\Pages\Partners .\src\EngineeringManager.Web\Pages\Crews .\src\EngineeringManager.Web\Pages\Projects .\tests\EngineeringManager.Tests\Application\OrganizationSummaryServiceTests.cs .\tests\EngineeringManager.Tests\Web\OrganizationSummaryLinkTests.cs
git commit -m "feat: add organization statistics and drill-down filters"
~~~

Expected: PASS, including crew construction-record union and BusinessPartnerId query parsing.

### Task 8: Preserve import/export and legacy data compatibility

**Files:**
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/EmployeeWorkbookImporter.cs
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookService.cs
- Modify: EngineeringManager/src/EngineeringManager.Application/DataExchange/* only where the existing contracts require the unified person number
- Modify: EngineeringManager/src/EngineeringManager.Infrastructure/Data/LegacyDataRepairService.cs
- Create: EngineeringManager/tests/EngineeringManager.Tests/Application/UnifiedPersonnelDataExchangeTests.cs
- Modify: EngineeringManager/tests/EngineeringManager.Tests/Application/DataExchangeRoundTripContractTests.cs

- [ ] **Step 1: Add regression tests**

Round-trip an employee and crew worker through the existing workbook services and assert the PersonNumber, scope, bridge IDs, and public fields survive; assert ambiguous identity numbers are reported rather than merged.

- [ ] **Step 2: Update import/export adapters**

Resolve existing rows by PersonNumber first, create a Person before any new legacy business row, route public field updates through PersonPublicDataSynchronizer, and keep all payroll/certificate/membership foreign keys unchanged. Export the unified person number and current scope as additional columns without removing old columns.

- [ ] **Step 3: Run round-trip tests and commit**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~UnifiedPersonnelDataExchangeTests|FullyQualifiedName~DataExchangeRoundTripContractTests|FullyQualifiedName~PersonnelMigrationMapTests" --no-restore
git add .\src\EngineeringManager.Infrastructure\DataExchange .\src\EngineeringManager.Application\DataExchange .\src\EngineeringManager.Infrastructure\Data\LegacyDataRepairService.cs .\tests\EngineeringManager.Tests\Application\UnifiedPersonnelDataExchangeTests.cs .\tests\EngineeringManager.Tests\Application\DataExchangeRoundTripContractTests.cs
git commit -m "feat: preserve personnel import and export compatibility"
~~~

Expected: PASS with no loss of existing workbook columns.

### Task 9: UI styling, responsive behavior, and browser verification

**Files:**
- Modify: EngineeringManager/src/EngineeringManager.Web/wwwroot/css/site.css (or the existing shared stylesheet containing employee-workspace/company-panel rules)
- Modify: EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/personnel-affiliation.js
- Create: EngineeringManager/tests/EngineeringManager.Tests/Web/PersonnelResponsiveUiTests.cs

- [ ] **Step 1: Add asset assertions**

Assert the new script is fingerprinted, selects have accessible labels, organization statistic anchors have visible text, and no new page introduces a fixed-width container.

- [ ] **Step 2: Implement responsive styles**

Use the existing design tokens, let metric cards wrap at 1366px/1440px, stack affiliation controls at 390px, and keep table wrappers horizontally scrollable rather than causing page-level overflow.

- [ ] **Step 3: Run the full automated suite**

~~~
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager.sln --configuration Release --no-restore --logger "console;verbosity=minimal"
dotnet build .\EngineeringManager.sln --configuration Release --no-restore --warnaserror
~~~

Expected: zero failed tests, zero skipped tests, zero build warnings/errors.

- [ ] **Step 4: Start the local app against the fixed test database only**

Use the repository’s existing local launch settings and EngineeringManager_Test connection string; apply the new migration with dotnet ef database update only to that test database. Never use a production connection string.

- [ ] **Step 5: Verify in the in-app browser**

Using a real logged-in session, exercise /Personnel/Internal, /Personnel/External, /Employees/Details, /Organization/Departments, company details, crew details, partner details, and project links. Check the four dependent selects, identity switching, latest-effective project refresh, statistic URL parameters, 1440px/1366px/390px layouts, console errors, failed network requests, and unauthorized target filtering.

- [ ] **Step 6: Commit verification-only fixes and finish**

Run the focused failing test for each discovered defect, apply the smallest fix, rerun it, then rerun the Release suite and browser smoke flow. Commit the final fixes:

~~~
$ErrorActionPreference = 'Stop'
git add .
git commit -m "test: verify unified personnel organization workflows"
~~~

## Self-review checklist

- Spec coverage: Tasks 2–3 cover the unified person/identity history and migration; Task 5 covers all four linked yellow-box fields and latest-effective project selection; Task 6 covers departments for companies, crews, and partners; Task 7 covers common statistics and exact project/personnel drill-downs; Task 4 covers internal/external navigation and switching; Task 8 preserves legacy import/export and foreign keys; Task 9 covers permissions, responsiveness, and browser acceptance.
- Placeholder scan: every step names a concrete file, API, command, and expected result; no step delegates an unspecified implementation.
- Type consistency: PersonId, PersonnelScope, ExternalPersonnelType, PersonnelEngagementHistory, PersonnelListQuery, OrganizationSummaryQuery, and BusinessPartnerId are introduced once and reused by later tasks.
- Safety: migrations are additive, legacy tables are retained, no production database is touched, and physical department deletion is never exposed for referenced rows.

After saving this plan, continue inline with superpowers:executing-plans; do not pause for an execution-choice prompt.
