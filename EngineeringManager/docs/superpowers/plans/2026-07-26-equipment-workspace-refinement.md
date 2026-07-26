# Equipment Workspace Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine equipment management so the summary focuses on company equipment composition, ownership supports self-owned/rented/other, dialogs match the company workspace, and usage history is viewed before it is edited.

**Architecture:** Extend the existing equipment aggregate without a schema migration: the enum receives a third persisted integer value, and the application service exposes read-only usage history rows for a selected business-year range. The Razor Page prepares portfolio/company summaries and the selected year's usage records; the existing page script filters records for the clicked equipment and only reveals the editor after an explicit edit or create action.

**Tech Stack:** ASP.NET Core Razor Pages, Entity Framework Core, vanilla JavaScript, project CSS design system, xUnit/FluentAssertions.

---

### Task 1: Specify the revised equipment workspace

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/EquipmentPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Application/EquipmentServiceTests.cs`

- [x] Add a web test requiring company composition rows, removal of certificate/status duplication, a dedicated ownership column, company-style row actions/dialogs, business-year usage history, and edit-gated usage fields.
- [x] Add an application test requiring `Other` ownership to save without owner/lessor and requiring usage history to return project/company labels within an inclusive date range.
- [x] Run the focused tests and confirm they fail because the new behavior is absent.

### Task 2: Extend ownership and usage query behavior

**Files:**
- Modify: `src/EngineeringManager.Domain/Equipment/EquipmentEnums.cs`
- Modify: `src/EngineeringManager.Application/Equipment/EquipmentDtos.cs`
- Modify: `src/EngineeringManager.Application/Equipment/IEquipmentService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Equipment/EquipmentService.cs`
- Modify: `src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs`
- Modify: `src/EngineeringManager.Web/Pages/Equipment/EquipmentEditorInput.cs`

- [x] Add `EquipmentOwnershipType.Other = 3`, map it to “其他”, and validate it without requiring an owner company or lessor.
- [x] Add a usage-history query returning equipment/project/company labels, dates, rate, rent mode, concurrency token, and existing periods.
- [x] Filter records whose entry/exit interval overlaps the selected business-year range and order newest first.
- [x] Run the focused application tests and confirm they pass.

### Task 3: Rebuild the equipment workspace presentation

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Equipment/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Equipment/Index.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/js/pages/equipment-workspace.js`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`

- [x] Load an unfiltered accessible portfolio dashboard, build a row for every accessible company (including zero counts), and expose total/self-owned/rented/other counts.
- [x] Resolve the selected business year to the configured current year, newest configured year, or calendar-year fallback, and load overlapping usage history.
- [x] Remove certificate-expiry metrics and the repeated bottom status list; make the sticky summary fill the viewport and add company composition bars.
- [x] Give ownership its own explicit archive column and make row actions match the company list's compact text-link layout.
- [x] Apply the company workspace dialog structure to equipment create/edit/details/usage dialogs.
- [x] Make the usage dialog open in history mode, filter rows to the clicked equipment, and reveal the form only after “新增记录” or “编辑”. Preserve existing period rows when editing.
- [x] Run the focused web tests and confirm they pass.

### Task 4: Verify behavior and presentation

**Files:**
- Verify: all files above

- [x] Run the focused equipment web and application test classes.
- [x] Run the solution build.
- [ ] Start the local app and verify the equipment page at desktop and mobile widths, including other ownership, full-height summary, compact actions, company-style dialogs, year filtering, history-first opening, and edit prefill. Desktop and mobile dialogs were verified; final post-restart mobile table screenshot is blocked by the expired browser login session.
- [x] Inspect the final diff for accidental edits to unrelated company work.
- [x] Add regression coverage for legacy usage authorization, inactive-company scope headings, and the default scope when unassigned equipment exists; confirm the tests fail before the fixes and pass afterward.
- [x] Run the final solution verification: build completed with 0 warnings and 0 errors, all 760 tests passed, and `git diff --check` passed.
