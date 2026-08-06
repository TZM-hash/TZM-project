# Unified Partner Directory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge construction crews and customer/general-contractor views into one categorized partner workspace, synchronize project-derived names and roles, and remove the partner/crew layout collisions.

**Architecture:** Keep `BusinessPartnerRoleType` as the only classification source. Add one idempotent infrastructure synchronizer for project-derived partner records, preserve legacy list routes through redirects, and let the Razor Page derive “all + three mutually exclusive categories” from roles without a database migration.

**Tech Stack:** ASP.NET Core Razor Pages, .NET 10, Entity Framework Core, xUnit, FluentAssertions, vanilla JavaScript, CSS, in-app Browser.

---

### Task 1: Add the project-derived partner directory synchronizer

**Files:**
- Create: `src/EngineeringManager.Application/Partners/IBusinessPartnerDirectorySynchronizer.cs`
- Create: `src/EngineeringManager.Infrastructure/Partners/BusinessPartnerDirectorySynchronizer.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`
- Test: `tests/EngineeringManager.Tests/Application/BusinessPartnerDirectorySynchronizerTests.cs`

- [ ] **Step 1: Write the failing synchronizer tests**

Create SQLite tests covering an existing unclassified project partner, a construction record, two serialized general-contractor names, and a second identical synchronization run:

```csharp
[Fact]
public async Task SynchronizeCreatesAndClassifiesProjectDerivedPartnersIdempotently()
{
    await using var fixture = await DirectoryFixture.CreateAsync();
    var imported = new BusinessPartner { PartnerNumber = "HZ0090", Name = "已有班组", ShortName = "已有班组" };
    var project = new Project
    {
        ProjectNumber = "XM-SYNC",
        Name = "同步项目",
        GeneralContractorName = ProjectGeneralContractors.Serialize(["甲方一有限公司", "总包二有限公司"])
    };
    project.Partners.Add(new ProjectPartner { Project = project, Partner = imported, RoleType = BusinessPartnerRoleType.ConstructionCrew });
    fixture.Db.AddRange(imported, project);
    await fixture.Db.SaveChangesAsync();

    await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);
    await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

    (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(3);
    (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew)).Should().Be(1);
    (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor)).Should().Be(2);
    (await fixture.Db.ProjectPartners.CountAsync()).Should().Be(3);
}
```

Add a second test proving duplicate full-name/short-name matches are skipped instead of guessed.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter FullyQualifiedName~BusinessPartnerDirectorySynchronizerTests
```

Expected: compilation fails because `IBusinessPartnerDirectorySynchronizer` and `BusinessPartnerDirectorySynchronizer` do not exist.

- [ ] **Step 3: Implement the interface and synchronizer**

Use this contract:

```csharp
public interface IBusinessPartnerDirectorySynchronizer
{
    Task SynchronizeAsync(Guid? projectId, CancellationToken cancellationToken);
}
```

The implementation must load the requested project or all projects, all partner names/short names, project partner links, and construction crew records. It must:

```csharp
EnsureRole(partner, BusinessPartnerRoleType.CustomerOrGeneralContractor);
EnsureProjectLink(project, partner, BusinessPartnerRoleType.CustomerOrGeneralContractor);
EnsureRole(projectPartner.Partner, projectPartner.RoleType);
EnsureRole(constructionRecord.CrewBusinessPartner, BusinessPartnerRoleType.ConstructionCrew);
```

Build a case-insensitive lookup that records ambiguous keys, generate the next unused `HZ0001`-style number, truncate names to entity limits, and call `SaveChangesAsync` once. Never modify existing master-data fields or activate an inactive partner.

Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<IBusinessPartnerDirectorySynchronizer, BusinessPartnerDirectorySynchronizer>();
```

- [ ] **Step 4: Run the focused tests and confirm GREEN**

Run the command from Step 2. Expected: all synchronizer tests pass.

- [ ] **Step 5: Commit Task 1**

```powershell
$ErrorActionPreference = 'Stop'
git add -- src/EngineeringManager.Application/Partners/IBusinessPartnerDirectorySynchronizer.cs src/EngineeringManager.Infrastructure/Partners/BusinessPartnerDirectorySynchronizer.cs src/EngineeringManager.Web/Program.cs tests/EngineeringManager.Tests/Application/BusinessPartnerDirectorySynchronizerTests.cs
git commit -m 'feat: synchronize project partner directory'
```

### Task 2: Make role edits move classifications and preserve import roles

**Files:**
- Modify: `src/EngineeringManager.Application/Partners/PartnerDtos.cs`
- Modify: `src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Test: `tests/EngineeringManager.Tests/Application/BusinessPartnerServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Application/StandardImportTests.cs`

- [ ] **Step 1: Write failing role-replacement and import tests**

Change the existing update test to assert the edited role is replaced while an unrelated third role remains:

```csharp
updated.Roles.Should().NotContain(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew);
updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.MaterialSupplier);
updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.MiscellaneousSupplier);
```

Pass `BusinessPartnerRoleType.ConstructionCrew` as the previous role. Add a standard import test whose workbook headers are `单位编号 / 单位名称 / 简称 / 业务角色`, imports `施工班组`, then updates the same unit to `甲方/总包` and asserts only the new role remains.

- [ ] **Step 2: Run the focused tests and confirm RED**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~BusinessPartnerServiceTests|FullyQualifiedName~StandardImportTests'
```

Expected: the service still retains the old edited role and the import mapping rejects `业务角色`.

- [ ] **Step 3: Implement previous-role replacement**

Append a backwards-compatible optional field to the request:

```csharp
public sealed record UpdateBusinessPartnerRequest(
    Guid Id,
    string PartnerNumber,
    string Name,
    string ShortName,
    string? UnifiedSocialCreditCode,
    string? Notes,
    PartnerRoleRequest Role,
    PartnerContactRequest? PrimaryContact,
    bool IsActive,
    Guid ConcurrencyStamp,
    string Reason,
    BusinessPartnerRoleType? PreviousRoleType = null);
```

In `UpdateAsync`, locate `PreviousRoleType ?? Role.RoleType`. If it differs from the requested role, either change that entity's `RoleType` or remove it when the target role already exists. Preserve all other roles. Then update the target role metadata.

- [ ] **Step 4: Add role parsing to standard import**

Add optional `业务角色 -> roles` to `Columns[ExportDataset.Partners]`. Parse separators `、`, `,`, `，`, `;`, `；` and accept both enum names and these labels:

```csharp
"甲方/总包" or "客户/总包" or "甲方" or "总包" => CustomerOrGeneralContractor
"施工班组" or "班组" => ConstructionCrew
"材料供应商" or "材料" => MaterialSupplier
"其他合作单位" or "其他供应商" or "零星供应商" => MiscellaneousSupplier
```

On new import, add distinct roles to the new partner. On update, replace the imported role set only when the `roles` column is present; otherwise preserve current roles for round-trip compatibility.

- [ ] **Step 5: Run focused tests and confirm GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit Task 2**

```powershell
$ErrorActionPreference = 'Stop'
git add -- src/EngineeringManager.Application/Partners/PartnerDtos.cs src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs tests/EngineeringManager.Tests/Application/BusinessPartnerServiceTests.cs tests/EngineeringManager.Tests/Application/StandardImportTests.cs
git commit -m 'feat: keep partner classifications in sync'
```

### Task 3: Build the unified categorized partner workspace and legacy redirects

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/js/pages/partner-workspace.js`
- Modify: `src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Test: `tests/EngineeringManager.Tests/Web/PartnerWorkspacePageModelTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`

- [ ] **Step 1: Write failing page-model and markup tests**

Add category tests for `all`, `crews`, `suppliers`, and `customers` using fake partners with single roles, multiple roles, and no roles. Assert the three small categories are mutually exclusive with `ConstructionCrew > CustomerOrGeneralContractor > supplier` priority. Update static page tests to require:

```text
data-partner-category-tabs
Category
施工班组
甲方/总包
材料供应商
PreviousRoleType
/Crews/Details
```

Update navigation tests to assert exactly one top-level `>合作单位</span>` entry and no top-level `>施工班组</span>` or `>甲方/总包</span>` entries. Add a crew page-model test asserting `/Crews` redirects to `/Partners?Category=crews`.

- [ ] **Step 2: Run focused web tests and confirm RED**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~PartnerWorkspacePage|FullyQualifiedName~ConstructionCrewPageTests'
```

Expected: category properties, tabs, previous-role field, redirect, and consolidated navigation are missing.

- [ ] **Step 3: Implement category derivation in the page model**

Add constants and a GET-bound category:

```csharp
public const string CrewCategory = "crews";
public const string CustomerCategory = "customers";
public const string SupplierCategory = "suppliers";
[BindProperty(SupportsGet = true)] public string? Category { get; set; }
```

Normalize the legacy `scope=customers` route into the customer category and legacy `category=other` into suppliers. `ApplyCategory` must implement the mutually exclusive design rules, and `CategorySummaries` must count against `AllPartners`. Historical synchronization runs at application startup so the GET handler remains read-only.

Pass `Editor.PreviousRoleType` into `UpdateBusinessPartnerRequest`, preserve `Category` in redirects and form routes, and stop rejecting customer roles on the unified page.

- [ ] **Step 4: Implement the tabs and unified row actions**

Render the accessible “全部 / 施工班组 / 材料供应商 / 甲方·总包” links before the workspace layout. For partners with a construction-crew role, add a `班组档案` action pointing to:

```csharp
Url.Page("/Crews/Details", new { id = partner.Id, ReturnUrl = returnUrl })
```

Do not default a no-role partner to `ConstructionCrew`; display `未分类` only in the all view. Add a hidden `Editor.PreviousRoleType` field and populate it from the edit payload in `partner-workspace.js`.

- [ ] **Step 5: Add legacy redirects and consolidate navigation**

Change `/Crews` GET to return:

```csharp
return RedirectToPage("/Partners/Index", new { Category = Partners.IndexModel.CrewCategory, Search, IsActive });
```

Keep crew POST handlers and `/Crews/Details` intact. Remove the separate customer and crew anchors from `_Layout.cshtml`; treat `/Partners`, `/Crews`, and partner detail routes as the one active partner navigation group.

- [ ] **Step 6: Run focused web tests and confirm GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 7: Commit Task 3**

```powershell
$ErrorActionPreference = 'Stop'
git add -- src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs src/EngineeringManager.Web/Pages/Partners/Index.cshtml src/EngineeringManager.Web/wwwroot/js/pages/partner-workspace.js src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml tests/EngineeringManager.Tests/Web/PartnerWorkspacePageModelTests.cs tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs
git commit -m 'feat: unify partner category workspace'
```

### Task 4: Run synchronization after project mutations and workbook imports

**Files:**
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`
- Test: `tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs`
- Test: `tests/EngineeringManager.Tests/Application/StandardImportTests.cs`

- [ ] **Step 1: Write failing integration tests**

Add tests that create/update a project's serialized general-contractor names and confirm matching partners, customer roles, and project links exist immediately after the operation. Add a project-workbook test where a partner row has a construction-crew role while the partner master has no role, then assert the role is repaired after confirm.

- [ ] **Step 2: Run integration tests and confirm RED**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~ProjectWorkspaceServiceTests|FullyQualifiedName~ProjectWorkbookImportTests|FullyQualifiedName~StandardImportTests'
```

Expected: project saves and imports do not yet invoke the synchronizer.

- [ ] **Step 3: Wire the synchronizer into project services**

Add an optional constructor dependency so existing focused tests remain source-compatible:

```csharp
IBusinessPartnerDirectorySynchronizer? partnerDirectorySynchronizer = null
```

After the project is saved but before its transaction commits, call:

```csharp
if (partnerDirectorySynchronizer is not null)
    await partnerDirectorySynchronizer.SynchronizeAsync(project.Id, cancellationToken);
```

Ensure project creation uses an explicit transaction so project and partner-directory writes commit or roll back together.

- [ ] **Step 4: Wire synchronization into both import paths**

Within the existing import transactions, instantiate `BusinessPartnerDirectorySynchronizer(db)` after project rows and project-detail rows have been saved:

```csharp
await new BusinessPartnerDirectorySynchronizer(db).SynchronizeAsync(null, cancellationToken);
```

For standard project imports, call it after project entities are persisted and before commit. For project workbook imports, call it after `WriteProjectDetailsAsync` and its save.

- [ ] **Step 5: Run integration tests and confirm GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit Task 4**

```powershell
$ErrorActionPreference = 'Stop'
git add -- src/EngineeringManager.Infrastructure/Projects/ProjectService.cs src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookImporter.cs src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs tests/EngineeringManager.Tests/Application/ProjectWorkbookImportTests.cs tests/EngineeringManager.Tests/Application/StandardImportTests.cs
git commit -m 'feat: sync partner directory from project changes'
```

### Task 5: Fix partner and crew responsive layout collisions

**Files:**
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Crews/Index.cshtml`
- Test: `tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`
- Test: `tests/EngineeringManager.Tests/Web/PersonnelResponsiveUiTests.cs`

- [ ] **Step 1: Write failing layout contract tests**

Require shared CSS rules for a 1280-pixel collapse, internal scrolling, normal whitespace only for clamped names, and single-line ellipsis for secondary fields:

```csharp
css.Should().Contain("@media (max-width: 1280px)")
    .And.Contain(".partner-workspace-layout, .crew-workspace-layout { grid-template-columns: 1fr; }")
    .And.Contain(".partner-name-clamp, .crew-name-clamp")
    .And.Contain("white-space: normal")
    .And.Contain(".partner-cell-ellipsis, .crew-cell-ellipsis")
    .And.Contain("text-overflow: ellipsis");
```

Require the role/trade/contact secondary spans to use the ellipsis classes.

- [ ] **Step 2: Run the focused UI tests and confirm RED**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~PartnerWorkspacePageTests|FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~PersonnelResponsiveUiTests'
```

Expected: the 1280 breakpoint and shared ellipsis rules are absent.

- [ ] **Step 3: Implement the responsive CSS**

Move the partner/crew layout collapse to `@media (max-width: 1280px)`, keep summary cards compact across the top, and use:

```css
.partner-name-clamp, .crew-name-clamp {
  white-space: normal;
  -webkit-line-clamp: 2;
}
.partner-cell-ellipsis, .crew-cell-ellipsis {
  display: block;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.partner-table-wrap, .crew-table-wrap {
  min-width: 0;
  max-width: 100%;
  overflow-x: auto;
}
```

Increase the partner/crew name column enough to prevent character-by-character wrapping and keep action buttons non-wrapping. Do not create page-level overflow.

- [ ] **Step 4: Run the focused UI tests and confirm GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 5: Commit Task 5**

```powershell
$ErrorActionPreference = 'Stop'
git add -- src/EngineeringManager.Web/wwwroot/css/pages.css src/EngineeringManager.Web/Pages/Partners/Index.cshtml src/EngineeringManager.Web/Pages/Crews/Index.cshtml tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs tests/EngineeringManager.Tests/Web/PersonnelResponsiveUiTests.cs
git commit -m 'fix: stabilize partner workspace layout'
```

### Task 6: Verify the complete feature

**Files:**
- Modify only if verification exposes a regression in files already listed above.

- [ ] **Step 1: Run formatting and build gates**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
dotnet build .\EngineeringManager.sln -c Release --no-restore -warnaserror
```

Expected: no diff errors, 0 warnings, 0 build errors.

- [ ] **Step 2: Run the full automated suite**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --no-build --no-restore
```

Expected: 0 failed tests.

- [ ] **Step 3: Verify only the test database**

Apply or inspect data only against `EngineeringManager_Test`. Confirm the synchronizer is idempotent there and do not connect to or mutate the production database.

- [ ] **Step 4: Run browser acceptance**

At 1440, 1366, and 390 pixels verify:

- `/Partners` has all plus the three mutually exclusive category tabs and no page-level horizontal overflow.
- Switching categories changes the rows and preserves search/status filters.
- Editing one unit from construction crew to another role moves it to the correct category after save.
- A construction crew row opens the retained crew detail page.
- `/Crews` and `/Partners/customers` reach the corresponding partner category.
- Table text does not overlap and horizontal scrolling remains inside the table wrapper.
- Console contains no application errors.

- [ ] **Step 5: Review the final diff and commit any verification fixes**

```powershell
$ErrorActionPreference = 'Stop'
git status --short
git diff --stat
git diff --check
```

If verification required no fixes, leave the five implementation commits unchanged. If it required scoped fixes, commit them as `fix: complete unified partner directory verification`.
