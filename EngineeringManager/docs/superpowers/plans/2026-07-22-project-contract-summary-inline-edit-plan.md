# Project Contract Summary Inline Edit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a compact 1-3 contract editor to the project overview, create a default main contract for new projects, and make collection contract choices use those contracts.

**Architecture:** Reuse the existing `Contracts` table and `ContractDto` data already loaded by the project workspace. Extend the existing project workspace update transaction with a contract quick-edit payload, auto-generate hidden contract numbers, and keep the first contract as the main/default contract. Render contract rows in the existing project quick-edit grid and continue feeding `item.Contracts` into the collection form.

**Tech Stack:** ASP.NET Core Razor Pages, C# records/services, EF Core SQL Server/SQLite tests, vanilla JavaScript, existing project workspace CSS.

---

### Task 1: Lock down contract synchronization and defaults with tests

**Files:**
- Modify: `tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectCollectionEntryPageTests.cs`

- [ ] **Step 1: Write failing service tests**

Add tests that create a project, submit one main and one additional quick-edit contract, then assert names, amounts, generated numbers, and the returned default contract order. Add a project creation test asserting a single contract is created with the project name and zero amount.

- [ ] **Step 2: Write failing page contract tests**

Assert that the project details page contains a contract quick-edit collection with name/amount inputs, an add-contract control, a contract total display, and that collection options are generated from `item.Contracts` with the first option selected by default.

- [ ] **Step 3: Run the focused tests and confirm the expected failures**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ProjectWorkspaceServiceTests|FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~ProjectCollectionEntryPageTests'
```

Expected: failures for missing contract quick-edit binding, missing default contract creation, and missing page markup.

### Task 2: Add contract quick-edit data flow

**Files:**
- Modify: `src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `src/EngineeringManager.Application/Projects/IProjectWorkspaceService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Edit.cshtml.cs`

- [ ] **Step 1: Add the request shape**

Extend `UpdateProjectRequest` with an optional `IReadOnlyCollection<ProjectContractQuickEditInput>` containing `Id`, `Name`, `TotalAmount`, and `ConcurrencyStamp`. Keep it optional so other callers and imports remain source-compatible.

- [ ] **Step 2: Synchronize contracts inside the existing project transaction**

In `ProjectWorkspaceService.UpdateAsync`, validate one to three submitted rows, require non-empty names and non-negative amounts, match existing IDs to the project, enforce concurrency stamps, update existing rows, and create new rows with generated numbers `<ProjectNumber>-C01`, `<ProjectNumber>-C02`, and `<ProjectNumber>-C03`. Mark the first row as `ContractType.MainContract`, later rows as `ContractType.Supplement`, set `AllocationMode` to `SingleCompany`, and store zero when the optional amount is blank.

- [ ] **Step 3: Create the default contract for new projects**

In `ProjectService.CreateProjectAsync`, add one `Contract` before saving with generated number `<ProjectNumber>-C01`, name equal to the normalized project name, `ContractType.MainContract`, `AllocationMode.SingleCompany`, and `TotalAmount = 0m`. Keep its amount display blank in the quick-edit UI when zero.

- [ ] **Step 4: Bind the quick-edit collection**

Add `List<ProjectContractQuickEditInput> Contracts` to `DetailsModel.QuickEditInput`, populate it from `Workspace.Contracts` ordered with the main contract first, and pass it through `UpdateProjectRequest` from `OnPostQuickEditAsync`. Populate `EditModel` with the default contract only for newly created projects through the service default; existing project editing remains unchanged.

- [ ] **Step 5: Re-run focused service tests**

Run the focused test command from Task 1. Expected: service tests pass; page tests remain red until the Razor/JS work is complete.

### Task 3: Implement the compact project overview editor

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `src/EngineeringManager.Web/wwwroot/js/site.js`

- [ ] **Step 1: Render the contract summary area**

Replace the current tax/equipment half-row with a full-width contract area that shows up to three rows. Each row uses one grid cell containing two aligned inputs: contract name and contract amount. Existing rows include hidden IDs/concurrency stamps; a blank extra row is available only while fewer than three contracts exist.

- [ ] **Step 2: Add client-side add/remove and amount total behavior**

Add a small vanilla JS initializer that adds one unsaved row up to three, removes only unsaved rows, recalculates the displayed contract total from all amount inputs, and preserves the normal quick-edit cancel/reset behavior.

- [ ] **Step 3: Replace the project summary amount label**

Change the yellow summary label from `合同金额` to `合同总额`, using the sum of the loaded contracts. Keep the value read-only in the overview and let the contract inputs be the only edit source.

- [ ] **Step 4: Keep collection contract options linked**

Ensure collection creation and collection batch-edit contract selects are built from the same `item.Contracts` list, show contract name/number, and select the first/main contract when no contract is already selected.

- [ ] **Step 5: Re-run focused web tests**

Run the focused test command and confirm all new page tests pass.

### Task 4: Verify regression behavior and desktop layout

**Files:**
- No new production files.

- [ ] **Step 1: Run the full test suite**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager.sln --configuration Release --no-restore
```

Expected: zero failed, zero skipped tests.

- [ ] **Step 2: Build Release**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager.sln --configuration Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Run desktop browser checks at 1440x900**

Verify the contract area renders in the former tax/equipment row, adding a second contract updates the total, the first contract is selected in collection creation, and the page has no viewport-level horizontal overflow.

- [ ] **Step 4: Run repository hygiene checks**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Do not stage or remove existing unrelated `test-results/` or `old-data/` content.
