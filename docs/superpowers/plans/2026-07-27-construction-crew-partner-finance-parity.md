# Construction Crew Partner Finance Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the construction crew table, finance data, and all dialogs match the partner workspace while preserving crew-only roster navigation.

**Architecture:** Keep the existing crew/partner row merge, then batch-load the same `PartnerLedgerSummaryDto` values used by the partner page through `ICentralLedgerQueryService`. Render the crew page with the partner table and dialog presentation classes, while using crew-scoped `data-*` hooks and a page-scoped script so partner behavior is not modified.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, existing central-ledger query service, native JavaScript, existing partner workspace CSS, xUnit, FluentAssertions.

**Execution constraints:** Execute inline in the current workspace. Do not create a worktree or subagent. Do not stage, commit, push, modify database schema, or change shared DTO/service contracts.

---

## File Map

- Modify `EngineeringManager/tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`: require partner-equivalent financial columns, dialogs, scripts, and model dependencies.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`: load ledger actor and batch partner financial summaries.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml`: render partner-equivalent financial cells and three dialogs.
- Modify `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/crew-workspace.js`: populate partner-equivalent details and finance dialogs.
- Modify `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`: synchronize the crew workbench column keys.
- Modify `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`: keep only crew-specific aliases and the wider five-action column.

### Task 1: Lock The Financial Parity Contract

- [ ] Add `CrewIndexMirrorsPartnerFinancialTableAndDialogs` to `ConstructionCrewPageTests.cs`.
- [ ] Require `receipts`, `payments`, and `invoices` columns; three financial progress bars; details/editor/finance dialogs; finance metrics/charts; `ICentralLedgerQueryService`; `ApplicationDbContext`; and `GetPartnerSummariesAsync`.
- [ ] Run the focused crew test and confirm RED because the current crew page has payroll totals instead of partner ledger summaries.

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter FullyQualifiedName~ConstructionCrewPageTests --no-restore
```

### Task 2: Load Partner Financial Summaries

- [ ] Inject `ICentralLedgerQueryService` and `ApplicationDbContext` into the crew page model.
- [ ] Add `CanViewFinance` with the same roles as the partner page and `PartnerFinancialSummaries` keyed by partner ID.
- [ ] After crew filtering, call `LedgerPageSupport.CreateActorAsync` and `GetPartnerSummariesAsync` once for the visible crew IDs.
- [ ] Keep create/update requests fixed to `BusinessPartnerRoleType.ConstructionCrew`.

### Task 3: Mirror The Partner Table And Dialog Markup

- [ ] Replace crew metric columns with role/trade, contact, projects, receivable, payable, invoice, status, and actions.
- [ ] Copy the partner page financial state, label, value, three-line amount, and progress markup using crew row data.
- [ ] Mirror the partner details and editor dialog structure; render the role as fixed read-only construction crew.
- [ ] Add the full partner finance dialog markup and route its jump action to `/Crews/Details?id=<id>#crew-finance`.
- [ ] Keep the personnel action routed to `/Crews/Details?id=<id>#crew-roster`.

### Task 4: Mirror Dialog Behavior And Presentation

- [ ] Extend `crew-workspace.js` with the partner finance number, state, chart, and zero-value behavior under crew-scoped hooks.
- [ ] Reuse partner workspace/table/editor/details/finance CSS classes in crew markup.
- [ ] Retain crew-only CSS for the 220px summary and 15rem five-action column.
- [ ] Update `DataWorkbenchPresets.Crews` to exactly match rendered keys.

### Task 5: Verify Regression And Visual Parity

- [ ] Run crew, partner, workbench, and responsive tests in Release.
- [ ] Run a Release solution build and `git diff --check`.
- [ ] Browser-check the crew page against the supplied partner screenshot: financial rows, progress colors, actions, all dialogs, roster/finance anchors, horizontal scrolling, clipping, and console errors.

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj -c Release --filter 'FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~PartnerWorkspacePageTests|FullyQualifiedName~ModuleDataWorkbenchTests|FullyQualifiedName~ResponsiveUiAssetTests' --no-restore
.\scripts\dotnet.ps1 build .\EngineeringManager.sln -c Release --no-restore
git diff --check
```
