# Project Overview Compact Quick Edit And Partners Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make project overview quick-edit selectors compact, prevent amount-view changes outside edit mode, and replace milestones with a unified related-party summary.

**Architecture:** Reuse the existing `selection-dropdown` and `data-check-selector` controls for legal entities and tax combinations. Add a focused presentation builder that combines the overview general contractor, active project partners, and construction crew records without changing persistence or application DTO contracts.

**Tech Stack:** ASP.NET Core Razor Pages, C#, existing ES modules and CSS, xUnit, FluentAssertions.

---

## Requirement Coverage

- 快捷编辑：签约公司、税金使用列管理式下拉复选菜单。
- 查看保护：工程金额仅在快捷编辑时显示可操作下拉框。
- 关联资料：删除项目里程碑，只显示项目人员和项目合作单位。
- 合作单位来源：总包、施工班组、材料供应商和零星供应商。
- 完成门禁：Release 全量测试、构建和桌面端验收；不做手机端验证。

---

### Task 1: Lock The Required Markup And Related-Party Rules

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Create: `tests/EngineeringManager.Tests/Web/ProjectRelatedPartyBuilderTests.cs`

- [ ] Add a failing markup test requiring two `data-check-selector` quick-edit dropdowns, `data-check-selector-clear`, a view-only amount label, an edit-only amount select, no “项目里程碑”, and exactly the personnel/party overview panels.
- [ ] Add failing builder tests with a general contractor, duplicated construction crew records, inactive project partners, and a supplier that appears from multiple sources. Require case-insensitive name deduplication, merged role labels, partner-note preference, and stable display ordering.
- [ ] Run:

```powershell
$ErrorActionPreference = 'Stop'
& .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter 'FullyQualifiedName~ProjectRelatedPartyBuilderTests|FullyQualifiedName~ProjectOverviewUsesCompactQuickEditSelectors'
```

Expected: failure because the builder and new markup do not exist.

### Task 2: Build Related-Party Presentation Data

**Files:**
- Create: `src/EngineeringManager.Web/Presentation/ProjectRelatedPartyBuilder.cs`
- Test: `tests/EngineeringManager.Tests/Web/ProjectRelatedPartyBuilderTests.cs`

- [ ] Add `ProjectRelatedPartyDisplay(string Name, IReadOnlyList<string> Roles, string? Notes)`.
- [ ] Add `ProjectRelatedPartyBuilder.Build(string? generalContractorName, IEnumerable<ProjectPartnerLinkDto> partners, IEnumerable<ProjectConstructionRecordDto> constructionRecords)`.
- [ ] Include only active project partners and construction records whose type is `ConstructionCrew`.
- [ ] Normalize names with trimming and `StringComparer.OrdinalIgnoreCase`, merge roles, prefer non-empty project-partner notes, then order total contractor first, crews second, and suppliers afterward.
- [ ] Re-run `ProjectRelatedPartyBuilderTests` and require all tests to pass.

### Task 3: Replace Expanded Quick-Edit Controls

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `src/EngineeringManager.Web/wwwroot/js/components/check-selector.js`
- Modify: `src/EngineeringManager.Web/wwwroot/js/components/quick-edit.js`

- [ ] Replace the legal-entity multiple select with a hidden edit control containing:

```html
<details class="selection-dropdown project-quick-selection" data-check-selector>
  <summary class="selection-dropdown-toggle"><span>签约公司</span><strong data-check-selector-count></strong></summary>
  <div class="selection-dropdown-menu">...</div>
</details>
```

- [ ] Replace the tax matrix with the same pattern, grouping checkbox labels by allowed tax rate and preserving `QuickEdit.TaxConfigurationSelections` values.
- [ ] Add `data-check-selector-clear` buttons to both menus and implement clearing all `data-check-selector-option` checkboxes followed by count refresh.
- [ ] Render the stage-derived amount label and value under `data-inline-edit-value`; render `data-project-amount-view` only inside a hidden `data-inline-edit-control` wrapper.
- [ ] On quick-edit cancellation, reset the form, close nested selector menus, and dispatch amount-select change so the stage-derived selection is restored.
- [ ] Add scoped CSS so dropdown menus overlay the panel and do not increase grid row height.

### Task 4: Replace Milestones With Unified Related Parties

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Test: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] Build `relatedParties` near the top of the Razor page from the general contractor, `item.Partners`, and `Model.ConstructionWorkspace.Records`.
- [ ] Remove the complete milestone panel.
- [ ] Keep the personnel panel unchanged.
- [ ] Render the party panel using the builder result, display merged role pills and notes, and show the deduplicated count.
- [ ] Re-run the focused markup and builder tests and require them to pass.

### Task 5: Verify The Complete Change

**Files:**
- No additional production files expected.

- [ ] Run focused project web tests.
- [ ] Run the complete Release test suite and read pass/fail counts from TRX.
- [ ] Run `& .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release --no-restore` and require zero warnings and errors.
- [ ] Start the Release app against `EngineeringManager_Test`.
- [ ] At 1440x900 verify both selector menus, amount control edit-state isolation, two related-data panels, general contractor/crew/supplier rendering, no horizontal overflow, and no console errors.
- [ ] Do not perform mobile verification. Do not commit, branch, merge, or push.
