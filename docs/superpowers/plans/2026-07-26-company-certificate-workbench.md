# Company Certificate Workbench Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix automatic company filtering on the equipment page and rebuild company certificates as a device-management-style workbench with consistent expiry reminders and modal interactions.

**Architecture:** Keep the existing certificate domain model and natural-month expiry calculator unchanged. Move company-certificate create, view, copy, edit, attachment preview, and delete interactions into the index page, backed by the existing certificate service; add page-scoped JavaScript and CSS that reuse the equipment workbench conventions.

**Tech Stack:** ASP.NET Core Razor Pages, C#, ES modules, native `dialog`, CSS Grid, xUnit, FluentAssertions, WebApplicationFactory.

---

### Task 1: Equipment Company Filter Regression

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/equipment-workspace.js`

- [ ] **Step 1: Write the failing asset test**

Assert that the equipment workspace script binds the `CompanyId` select change event and calls `requestSubmit()` on `.workbench-inline-filters`.

```csharp
equipmentScript.Should().Contain("[name=\"CompanyId\"]")
    .And.Contain("addEventListener(\"change\"")
    .And.Contain("requestSubmit()");
```

- [ ] **Step 2: Run the targeted test and verify it fails**

Run:

```powershell
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ResponsiveUiAssetTests --no-restore
```

Expected: the new automatic company-filter assertion fails because no change handler exists.

- [ ] **Step 3: Implement the smallest page-scoped fix**

Bind only the equipment `CompanyId` select and submit its existing GET form:

```javascript
const companySelect = form.querySelector('[name="CompanyId"]');
companySelect?.addEventListener("change", () => form.requestSubmit());
```

- [ ] **Step 4: Re-run the test and browser reproduction**

Expected: the asset test passes; selecting a company updates the URL with `CompanyId` and reduces the equipment rows to that company's records.

### Task 2: Company Certificate Workbench Contract

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/CertificatePageTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Certificates/Index.cshtml.cs`

- [ ] **Step 1: Write failing page tests**

Assert the rendered page contains the workbench layout, automatic company filter hook, Mac-style editor/detail/delete dialogs, semantic action buttons, attachment preview, and inline editor fields. Assert read-only roles cannot see mutation controls.

```csharp
companyHtml.Should().Contain("data-company-certificate-workspace")
    .And.Contain("data-company-certificate-editor-dialog")
    .And.Contain("data-company-certificate-details-dialog")
    .And.Contain("data-company-certificate-delete-dialog")
    .And.Contain("action-button--view")
    .And.Contain("action-button--edit")
    .And.Contain("action-button--copy")
    .And.Contain("action-button--certificate");
```

- [ ] **Step 2: Run the targeted test and verify it fails**

Run:

```powershell
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~CertificatePageTests --no-restore
```

Expected: the workbench and modal contract assertions fail against the current navigation-based page.

- [ ] **Step 3: Add index-page handlers and view models**

Add bound editor and delete inputs, `OnPostSaveAsync`, `OnPostDeleteAsync`, safe attachment validation, model-state isolation, dialog restoration after validation errors, and portfolio/company summary construction. Continue to call the existing `ICompanyCertificateService` methods without changing persistence or the shared expiry calculator.

- [ ] **Step 4: Keep the status mapping shared and explicit**

Map existing states consistently everywhere:

```csharp
LongTerm => "长期有效";
Normal => "有效";
Info => "轻度提醒";
Warning => "中度提醒";
Critical => "重度提醒";
Expired => "已过期";
```

### Task 3: Company Certificate Workbench UI

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Certificates/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/company-certificate-workspace.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] **Step 1: Replace the vertical list with an equal-height workbench**

Render left-side status totals and per-formal-company certificate composition; render the right-side filtered certificate archive. Reuse the existing data-workbench search/status controls and make the company select submit immediately.

- [ ] **Step 2: Add semantic one-line actions**

Render `查看`, `编辑`, `复制`, and `附件` with the established view/edit/copy/certificate colors. Keep delete out of the table row.

- [ ] **Step 3: Add horizontal Mac-style dialogs**

Create read-only detail, create/edit/copy editor, typed delete confirmation, and attachment-preview dialogs. Copy company/type/scope/issuer only; clear certificate number, issue/expiry dates, and attachment.

- [ ] **Step 4: Add page-scoped interaction code**

Populate dialogs from JSON row payloads, restore server-opened dialogs after validation errors, auto-submit the company filter, and initialize the shared attachment preview component.

- [ ] **Step 5: Add responsive styles**

Use the equipment workspace proportions, 8px card/dialog radii, fixed single-row operation width, overflow-safe long company names, equal-height columns, and a one-column narrow-screen fallback.

### Task 4: Verification

**Files:**
- Modify as needed: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`
- Modify as needed: `EngineeringManager/tests/EngineeringManager.Tests/Web/CertificatePageTests.cs`

- [ ] **Step 1: Run focused tests**

```powershell
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CertificatePageTests|FullyQualifiedName~ResponsiveUiAssetTests|FullyQualifiedName~CertificateExpiryCalculatorTests" --no-restore
```

- [ ] **Step 2: Build Release**

```powershell
dotnet build EngineeringManager/EngineeringManager.sln -c Release --no-restore
```

- [ ] **Step 3: Verify in the browser**

Reload the local app, confirm equipment company selection updates rows and URL, then test company-certificate filtering, all dialogs, copy clearing rules, attachment preview, status colors, desktop layout, and narrow-screen layout.

- [ ] **Step 4: Review the final diff**

Confirm the pre-existing equal-height changes in `pages.css` and `ResponsiveUiAssetTests.cs` remain intact and no unrelated files changed.
