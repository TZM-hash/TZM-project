# Customer / General Contractor Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the customer/general-contractor role out of the general partner workspace and expose it as a dedicated navigation entry above construction crews.

**Architecture:** Reuse `Pages/Partners/Index` through an optional route scope (`/Partners/customers`). The page model owns scope enforcement and data isolation; the Razor view only changes presentation, available filters, editor controls, and workbench identity from that model state. The default `/Partners` route excludes every partner carrying the customer/general-contractor role.

**Tech Stack:** ASP.NET Core Razor Pages, C#, EF-backed partner application service, vanilla JavaScript, xUnit, FluentAssertions.

---

### Task 1: Lock the navigation and workspace contract with tests

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`
- Read: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`

- [ ] **Step 1: Add a failing source contract test**

Assert that the partner page accepts a `customers` route scope, the model exposes `IsCustomerScope`, the general workspace excludes `CustomerOrGeneralContractor`, and the layout renders `甲方/总包` before `施工班组`.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~CustomerGeneralContractorUsesDedicatedScopedWorkspace'
```

Expected: FAIL because the route scope and navigation entry do not exist.

### Task 2: Add the scoped partner workspace

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/partner-workspace.js`

- [ ] **Step 1: Add scope state and role enforcement to the page model**

Add `Scope`, `IsCustomerScope`, and a scope-aware load predicate. In customer scope, force `Editor.RoleType` to `CustomerOrGeneralContractor`; in general scope, reject that role and remove customer/general-contractor partners from `AllPartners`, `Partners`, and `RoleSummaries`.

- [ ] **Step 2: Make the Razor workspace scope-aware**

Use `@page "{scope?}"`. In customer scope render `甲方/总包` titles, omit the role filter, lock the editor role, use a dedicated page key/table id, and preserve `scope` through POST redirects. In general scope remove `CustomerOrGeneralContractor` from role filter and editor options.

- [ ] **Step 3: Give create/copy dialogs the correct default role**

Expose `data-default-role` and `data-entity-label` on the workspace root. Read both in `partner-workspace.js` so the customer view creates role `1`, while the general partner view continues to default to construction crew role `2`.

- [ ] **Step 4: Run focused workspace tests and verify GREEN**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~PartnerWorkspacePageTests'
```

Expected: all partner workspace tests pass.

### Task 3: Add navigation and verify the user workflow

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`

- [ ] **Step 1: Add the dedicated navigation entry**

Render `甲方/总包` immediately before `施工班组`, link it to `/Partners/customers`, make `/Partners` exact-active, and return to the scoped page from scoped child navigation.

- [ ] **Step 2: Verify both routes in the browser**

Check `/Partners/customers` shows only customer/general-contractor rows and no role selector. Check `/Partners` has no customer/general-contractor option or rows. Open create/edit/finance dialogs on both routes and confirm the customer role remains locked in the dedicated view.

- [ ] **Step 3: Run final verification**

Run the partner/crew web tests, JavaScript syntax check, Release build, and `git diff --check`. Keep the Release service available on port `5075`.
