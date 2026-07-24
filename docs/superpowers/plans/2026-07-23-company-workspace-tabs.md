# 自有公司工作台（下拉 + 标签页）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把「自有公司」改成可在全部公司与具体公司间下拉切换的经营工作台；具体公司用 6 个标签展示概览、档案与只读业务明细，业务编辑通过超链接进入原模块。

**Architecture:** 保持分离路由：`/Companies` 为全部公司总览，`/Companies/Details/{id}?tab=` 为具体公司工作台。扩展 `ICompanyManagementService` 增加公司维度只读聚合查询；档案写入继续复用现有 `SaveCompanyAsync` / `SaveAccountAsync` / `SaveCertificateAsync`。页面层按 `tab` 按需加载，不把全部业务一次塞进 `IndexModel`。

**Tech Stack:** .NET、ASP.NET Core Razor Pages、EF Core、xUnit、FluentAssertions、现有 inline-edit 与 panel/table CSS。

**Spec:** `docs/superpowers/specs/2026-07-23-company-workspace-tabs-design.md`

**Execution constraints:**
- 串行 TDD；优先本会话执行或按用户选择的 subagent 方式。
- 不创建 git worktree，除非用户另行要求。
- 不执行 Git 暂存/提交/推送，除非用户明确要求。
- 所有 PowerShell 命令以 `$ErrorActionPreference = 'Stop'` 开头。
- 本轮无数据库迁移（仅查询与 UI/服务逻辑）。

---

## 文件结构

### 修改（主路径）

| 文件 | 职责 |
| --- | --- |
| `EngineeringManager/src/EngineeringManager.Application/Companies/CompanyDtos.cs` | 新增工作台只读 DTO |
| `EngineeringManager/src/EngineeringManager.Application/Companies/ICompanyManagementService.cs` | 新增只读查询方法 |
| `EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs` | 实现查询；停用账户时清默认用途 |
| `EngineeringManager/src/EngineeringManager.Domain/Organization/CompanyCategory.cs` | 可选：账户停用规则辅助方法（或写在服务内） |
| `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml` | 2 指标卡 + 公司下拉框架 |
| `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml.cs` | 总览数据；下拉选项 |
| `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml` | 标签壳 + 6 个内容区 |
| `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml.cs` | `Tab` 绑定、按 tab 加载、保留档案 POST |
| `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_CompanyScopeSwitcher.cshtml` | 共用公司范围下拉 partial |
| `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css` | 标签导航、停用账户行样式 |
| `EngineeringManager/tests/EngineeringManager.Tests/Application/CompanyManagementServiceTests.cs` | 服务层测试 |
| `EngineeringManager/tests/EngineeringManager.Tests/Web/CompanyPageTests.cs` | 页面集成测试与 Fake 扩展 |
| `docs/开发进度.md` | 记录本轮功能迭代 |

### 不修改

- 项目财务写入服务、中央账本写入逻辑
- 账户/公司物理删除
- 新迁移

---

### Task 1: 新增工作台只读 DTO 与接口方法

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Companies/CompanyDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Companies/ICompanyManagementService.cs`
- Test: 先扩展 Fake 编译（Task 6 完整页面测试）；本任务以编译通过为门禁

- [ ] **Step 1: 在 `CompanyDtos.cs` 末尾追加 DTO**

```csharp
public sealed record CompanyWorkspaceSummaryDto(
    int ProjectCount,
    int ContractCount,
    int ActiveAccountCount,
    int TotalAccountCount,
    int ValidCertificateCount,
    int TotalCertificateCount,
    int ExpiredCertificateCount);

public sealed record CompanyActivityItemDto(
    string Kind,           // project | contract | collection | payment | invoice
    string Title,
    string? Subtitle,
    decimal? Amount,
    DateOnly? Date,
    Guid? ProjectId,
    Guid? EntityId);

public sealed record CompanyProjectRowDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    string Stage,
    decimal CompanyContractAmount,
    decimal ReceivableAmount,
    decimal CollectedAmount,
    decimal PayableAmount,
    decimal PaidAmount);

public sealed record CompanyContractRowDto(
    Guid ContractId,
    Guid ProjectId,
    string ContractNumber,
    string ContractName,
    decimal ContractTotalAmount,
    decimal CompanyShareAmount,
    decimal? CompanySharePercentage,
    bool IsActive);

public sealed record CompanyCollectionRowDto(
    Guid Id,
    DateOnly Date,
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    string Summary,
    Guid AccountId,
    string AccountName,
    bool AccountIsActive,
    decimal Amount);

public sealed record CompanyPaymentRowDto(
    Guid Id,
    DateOnly Date,
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    string Summary,
    Guid AccountId,
    string AccountName,
    bool AccountIsActive,
    decimal Amount);

public sealed record CompanyInvoiceRowDto(
    Guid Id,
    string Direction,      // Output | Input 或 销项 | 进项（服务层返回中文）
    string InvoiceNumber,
    DateOnly InvoiceDate,
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    string LegalEntityName,
    decimal GrossAmount);
```

- [ ] **Step 2: 扩展 `ICompanyManagementService`**

```csharp
Task<CompanyWorkspaceSummaryDto> GetWorkspaceSummaryAsync(CompanyActor actor, Guid companyId, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyActivityItemDto>> ListRecentActivityAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyProjectRowDto>> ListCompanyProjectsAsync(CompanyActor actor, Guid companyId, string? search, int take, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyContractRowDto>> ListCompanyContractsAsync(CompanyActor actor, Guid companyId, Guid? projectId, int take, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyCollectionRowDto>> ListCompanyCollectionsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyPaymentRowDto>> ListCompanyPaymentsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
Task<IReadOnlyList<CompanyInvoiceRowDto>> ListCompanyInvoicesAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
```

- [ ] **Step 3: 在 `CompanyManagementService` 先加抛 `NotImplementedException` 的方法体（下一步实现），保证接口可编译**

- [ ] **Step 4: 编译 Application + Infrastructure**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager/src/EngineeringManager.Application/EngineeringManager.Application.csproj
dotnet build EngineeringManager/src/EngineeringManager.Infrastructure/EngineeringManager.Infrastructure.csproj
```

Expected: 成功（若 Fake 未更新导致测试项目失败可先忽略测试项目，下一步再改）。

---

### Task 2: 停用账户清除默认用途（服务规则 + 测试）

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs`（`SaveAccountAsync`）
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/CompanyManagementServiceTests.cs`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task DeactivatingAccountClearsDefaultFlags()
{
    await using var scope = await CreateScopeAsync();
    var company = new LegalEntity { Code = "LE-DEF", Name = "默认账户公司", ShortName = "默认" };
    scope.Db.Add(company);
    await scope.Db.SaveChangesAsync();
    var actor = CompanyActor.Administrator("admin");
    var created = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
        null, company.Id, "默认户", "1", "行", (int)FinancialAccountType.Bank, 0m,
        true, true, true, true, null, "新增"), default);

    var updated = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
        created.Id, company.Id, created.AccountName, "1", "行", (int)FinancialAccountType.Bank, 0m,
        true, true, true, false, created.ConcurrencyStamp, "停用公司账户"), default);

    updated.IsActive.Should().BeFalse();
    updated.IsDefaultCollection.Should().BeFalse();
    updated.IsDefaultPayment.Should().BeFalse();
    updated.IsDefaultInvoice.Should().BeFalse();
}
```

- [ ] **Step 2: 运行测试确认失败或旧行为不符合断言**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~DeactivatingAccountClearsDefaultFlags"
```

- [ ] **Step 3: 在 `SaveAccountAsync` 赋值默认标记处增加**

```csharp
var isActive = request.IsActive;
account.IsActive = isActive;
account.IsDefaultCollection = isActive && request.IsDefaultCollection;
account.IsDefaultPayment = isActive && request.IsDefaultPayment;
account.IsDefaultInvoice = isActive && request.IsDefaultInvoice;
```

（替换原先直接赋值 `request.IsDefault*` / `request.IsActive` 的片段，保持其余逻辑不变。）

- [ ] **Step 4: 再跑测试，Expected: PASS**

- [ ] **Step 5: 确认 `OnPostAccountStatusAsync` 在停用时已传 `isActive && account.IsDefault*`（Details 页已有类似逻辑）；若仅调用 `SaveAccountAsync` 则服务层规则已足够。**

---

### Task 3: 实现公司工作台只读查询

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/CompanyManagementServiceTests.cs`

- [ ] **Step 1: 写集成向服务测试（Sqlite 内存/现有 CreateScopeAsync）**

```csharp
[Fact]
public async Task WorkspaceQueriesReturnCompanyScopedRows()
{
    await using var scope = await CreateScopeAsync();
    // 准备：LegalEntity、Project、ProjectLegalEntity、Contract、ContractLegalEntityAllocation、
    // ReceivableEntry、CollectionEntry、PaymentEntry、InvoiceEntry、FinancialAccount
    // 断言：GetWorkspaceSummaryAsync 项目数/合同数正确
    // ListCompanyProjectsAsync 含本公司份额
    // ListCompanyCollectionsAsync / Payments / Invoices 仅本公司 LegalEntityId
    // ListRecentActivityAsync Count <= take 且按日期倒序
}
```

测试数据尽量最小：1 公司、1 项目、1 合同份额、1 收款、1 付款、1 销项发票。

- [ ] **Step 2: 跑测试 Expected: FAIL（方法未实现或返回空）**

- [ ] **Step 3: 实现查询（要点）**

`GetWorkspaceSummaryAsync`:
- `EnsureAccessAsync`
- 项目：`ProjectLegalEntities` 或 合同分配触及的 `ProjectId` 去重计数
- 合同：`ContractLegalEntityAllocations` where `LegalEntityId`
- 账户：active/total
- 证书：`CompanyCertificates` where `!IsDeleted`；过期 = `ExpiresOn < today`

`ListCompanyProjectsAsync`:
- 项目集合 = 作为 `ProjectLegalEntity` 或存在本公司合同分配的项目
- `CompanyContractAmount` = 该项目下本公司 `ContractLegalEntityAllocation` 份额之和（`Amount ?? TotalAmount * Percentage/100`）
- 应收/已收/应付/已付：按 `LegalEntityId == companyId` 汇总（与 `GetDashboardAsync` 同源表：`ReceivableEntries`、`CollectionEntries`、退款表若 dashboard 已用则一致、`PayableEntries`、`PaymentEntries`/`PaymentReversalEntries`）
- `search`：编号/名称 Contains
- `OrderByDescending` 最近更新或编号，`Take(take)` 默认 50

`ListCompanyContractsAsync`:
- 从 `ContractLegalEntityAllocations` include `Contract`、`Contract.Project`
- 映射编号、名称、总额、份额、比例、`IsActive`

`ListCompanyCollectionsAsync` / `Payments` / `Invoices`:
- `Where LegalEntityId == companyId && !void`（收款无 void 字段则不过滤 void；应收 void 不在此列表）
- Include Project、Account
- 账户名 + `AccountIsActive`
- `OrderByDescending` 日期，`Take(take)`

`ListRecentActivityAsync`:
- 合并项目创建/合同/收/付/票各取若干后在内存按日期排序 `Take(10)`
- `Kind`/`Title`/`EntityId`/`ProjectId` 填全以便 UI 链接

- [ ] **Step 4: 跑测试 Expected: PASS**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyManagementServiceTests"
```

---

### Task 4: 共用公司范围下拉 Partial + 全部公司页指标卡

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_CompanyScopeSwitcher.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml.cs`
- Test: `CompanyPageTests.AdministratorSeesCompanyDashboardAndDirectAmounts` 调整断言

- [ ] **Step 1: 更新页面测试断言（先写期望）**

在 `AdministratorSeesCompanyDashboardAndDirectAmounts` 中：
- 保留 `data-company-dashboard`、`测试自有公司`、`新增公司`
- 增加：`data-company-scope-switcher`、`全部公司`
- 指标：页面应显示「公司数量」「未收款」
- **删除/避免**对「账户余额」作为指标卡标签的依赖；金额对比面板仍可出现收款等数字 `1,000.00` 等

Finance 只读测试保持可访问、无「新增公司」。

- [ ] **Step 2: 跑相关测试 Expected: FAIL**

- [ ] **Step 3: 创建 `_CompanyScopeSwitcher.cshtml`**

Model 建议匿名或小型 view model：
- `IReadOnlyList<CompanyListItemDto> Companies`
- `Guid? SelectedCompanyId`（null = 全部公司）
- `string? CurrentTab`（详情页传入，总览传 null）

行为：
- `<select data-company-scope-switcher>` 或 form GET
- 选项 0：`value=""` 文本 `全部公司` → `/Companies`
- 其余：`/Companies/Details/{id}` + 若有 `CurrentTab` 则 `?tab=`
- 使用少量 JS：`onchange` 时 `window.location = option.dataset.href`（与站内其他 switcher 风格一致即可）

- [ ] **Step 4: 改 `Index.cshtml` 指标卡仅两张**

```html
<article class="metric-card"><span class="metric-label">公司数量</span><strong class="metric-value">@dashboard.CompanyCount</strong></article>
<article class="metric-card"><span class="metric-label">未收款</span><strong class="metric-value">@((dashboard.ReceivableAmount - dashboard.CollectedAmount).ToString("N2"))</strong></article>
```

移除合同金额、账户余额指标卡。保留金额对比面板。标题区加入 partial 下拉。

- [ ] **Step 5: 跑 `CompanyPageTests` 中 Index 相关用例 Expected: PASS**

---

### Task 5: Details 页模型 — Tab 与按需加载

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml.cs`

- [ ] **Step 1: 增加属性**

```csharp
[BindProperty(SupportsGet = true)]
public string? Tab { get; set; }

public string ActiveTab => NormalizeTab(Tab);
public IReadOnlyList<CompanyListItemDto> CompanyOptions { get; private set; } = [];
public CompanyWorkspaceSummaryDto? WorkspaceSummary { get; private set; }
public IReadOnlyList<CompanyActivityItemDto> RecentActivity { get; private set; } = [];
public IReadOnlyList<CompanyProjectRowDto> Projects { get; private set; } = [];
public IReadOnlyList<CompanyContractRowDto> Contracts { get; private set; } = [];
public IReadOnlyList<CompanyCollectionRowDto> Collections { get; private set; } = [];
public IReadOnlyList<CompanyPaymentRowDto> Payments { get; private set; } = [];
public IReadOnlyList<CompanyInvoiceRowDto> Invoices { get; private set; } = [];
public string? ProjectSearch { get; set; } // SupportsGet 可选
```

```csharp
private static string NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
{
    "profile" => "profile",
    "certificates" => "certificates",
    "accounts" => "accounts",
    "projects" => "projects",
    "finance" => "finance",
    _ => "overview"
};
```

- [ ] **Step 2: 改 `LoadAsync`**

公共：`GetAsync`、`GetDashboardAsync`、`ListAsync`（下拉）、管理权限时 categories / QuickEdit。

按 `ActiveTab`：
- `overview` → summary + activity(10)
- `profile` →（档案已在 GetAsync）
- `certificates` → 使用 `Company.Certificates`
- `accounts` → `Company.Accounts` 排序：启用在前
- `projects` → projects + contracts（take 50）
- `finance` → collections/payments/invoices（各 50）

- [ ] **Step 3: 所有 RedirectToPage 带上 tab**

```csharp
return RedirectToPage(new { id, tab = ActiveTab });
// 或显式 "accounts" / "profile" / "certificates"
```

`OnPostQuickEditAsync` → `tab=profile`  
`OnPostAccount*` → `tab=accounts`  
`OnPostCertificateAsync` → `tab=certificates`  
`OnPostAccountStatusAsync`：reason 文案改为「停用公司账户」/「启用公司账户」；确认逻辑保持 `SaveAccountAsync`。

- [ ] **Step 4: 无权限/不存在：`KeyNotFoundException` 时返回 NotFound 或中文错误页（与现网一致即可）**

---

### Task 6: Details 视图 — 壳、标签、档案与业务只读

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Test: `CompanyPageTests` 增加详情用例

- [ ] **Step 1: 页面测试**

```csharp
[Fact]
public async Task AdministratorCompanyDetailsShowsTabsAndScopeSwitcher()
{
    await using var factory = CreateFactory("ApplicationAdministrator");
    using var client = factory.CreateClient();
    var id = FakeCompanyService.CompanyId;
    var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{id}?tab=overview"));
    html.Should().Contain("data-company-scope-switcher");
    html.Should().Contain("data-company-tabs");
    html.Should().Contain("经营概览");
    html.Should().Contain("基本信息");
    html.Should().Contain("证书信息");
    html.Should().Contain("账户信息");
    html.Should().Contain("项目与合同");
    html.Should().Contain("收付款与发票");
    html.Should().Contain("未收款");
    html.Should().Contain("未付款");
}

[Fact]
public async Task AdministratorAccountsTabShowsEnableDisableLabelsNotDelete()
{
    // Fake 返回一个启用账户；HTML 应含「停用」按钮文案路径或 data 属性
    var html = await client.GetStringAsync($"/Companies/Details/{id}?tab=accounts");
    html.Should().Contain("停用"); // 或 data-account-status-action
    html.Should().NotContain("确认删除这个账户吗");
}

[Fact]
public async Task FinanceCanOpenDetailsButNotManageAccounts()
{
    // 可打开 Details；不应含「快捷编辑公司」「保存账户」等管理控件
}
```

扩展 `FakeCompanyService` 实现所有新接口，返回少量假数据（项目 1 条、收款 1 条等）。

- [ ] **Step 2: 跑测试 Expected: FAIL**

- [ ] **Step 3: 重写 `Details.cshtml` 结构（保留现有 inline-edit 字段绑定名）**

骨架顺序：
1. `page-heading` + 下拉 partial（SelectedCompanyId = Company.Id, CurrentTab = ActiveTab）
2. `overview-strip`：应收/已收、应付/已付、未收款、未付款（**不要**合同额、账户余额）
3. `nav.company-tabs` `data-company-tabs`：6 个链接 `asp-page` + `asp-route-id` + `asp-route-tab`
4. 按 `Model.ActiveTab` 渲染一块内容：

**overview:** 对比面板 + 结构摘要 + 动态表（链接：项目 → `/Projects/Details/{id}`）

**profile:** 迁移现有「基础与开票资料」inline-edit 面板

**certificates:** 现有证书表 + 新增表单（从旧页迁移）；空态文案按规格

**accounts:** 现有账户表；行增加：
- `class="@(account.IsActive ? null : "is-account-inactive")"`
- 名称后 `@if (!account.IsActive) { <span class="status-badge status-badge--muted">已停用</span> }`
- 状态列徽章
- 启停按钮文案「停用」/「启用」；`onclick` confirm 用规格中文案
- 排序：启用在前（可在视图 `OrderByDescending(a => a.IsActive).ThenBy(a => a.AccountName)`）

**projects:** 表格 + 项目链接 `asp-page="/Projects/Details" asp-route-id`；合同子表或第二表

**finance:** 三块表；摘要链接优先：
- 项目列 → Project Details
- 单据：`/Projects/Details/{projectId}`（可附 fragment 若项目页有；否则仅详情）
- 发票号同理
- 账户名：若 `!AccountIsActive` 追加「已停用」

- [ ] **Step 4: CSS（`pages.css`）**

```css
.company-tabs { display:flex; flex-wrap:nowrap; gap:.35rem; overflow-x:auto; margin:.75rem 0; }
.company-tabs a { white-space:nowrap; padding:.45rem .75rem; border-radius:999px; text-decoration:none; color:var(--app-muted); border:1px solid transparent; }
.company-tabs a.is-active { color:var(--app-text); background:var(--app-primary-soft); border-color:rgba(37,99,235,.34); font-weight:600; }
tr.is-account-inactive { opacity:.72; background:rgba(15,23,42,.04); }
.status-badge--muted { display:inline-block; margin-left:.35rem; padding:.05rem .4rem; font-size:.72rem; border:1px solid var(--app-border); border-radius:999px; color:var(--app-muted); }
.status-badge--ok { /* 启用绿意 */ }
```

- [ ] **Step 5: 更新 Fake 并跑 `CompanyPageTests` Expected: PASS**

---

### Task 7: 业务深链与返回体验（最小可用）

**Files:**
- Modify: `Details.cshtml`（projects/finance 链接）
- Optional: 项目/财务页无需改即可验收「能打开」

- [ ] **Step 1: 统一链接**

| 字段 | href |
| --- | --- |
| 项目编号/名称 | `/Projects/Details/{projectId}` |
| 合同 | `/Projects/Details/{projectId}`（同项目；无独立合同路由） |
| 收款/付款行 | `/Projects/Details/{projectId}` |
| 发票号 | `/Projects/Details/{projectId}` |
| 查看更多 | `/Projects?LegalEntityId={companyId}` 与 `/Finance`（Finance 若无公司筛，仅文案链到财务首页） |

- [ ] **Step 2: 详情标题区增加「返回全部公司」→ `/Companies`**

- [ ] **Step 3: 手动或测试断言 HTML 含 `/Projects/Details/` 与 `LegalEntityId`（项目列表更多链接）**

---

### Task 8: 回归服务/页面测试与手工检查清单

**Files:**
- Tests only + `docs/开发进度.md`

- [ ] **Step 1: 跑公司相关测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~Company"
```

Expected: 全部 PASS

- [ ] **Step 2: 构建 Web**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager/src/EngineeringManager.Web/EngineeringManager.Web.csproj -c Release
```

- [ ] **Step 3: 对照规格验收清单（手工）**

1. `/Companies` 仅 2 指标卡  
2. 下拉切换公司/全部  
3. 6 标签 URL `tab`  
4. 账户停用视觉与文案  
5. 业务 tab 无保存、有超链接  
6. Finance 角色只读  

- [ ] **Step 4: 更新 `docs/开发进度.md`**

在「功能迭代」表增加一行：自有公司工作台（下拉 + 标签页）— 进行中/已完成；并指向 spec 与 plan 路径。

---

## 实现时注意点

1. **不要**在 Index 指标卡恢复合同金额/账户余额。  
2. **不要**在公司页实现收付款保存 handler。  
3. 账户列表**必须**显示停用行。  
4. `GetDashboardAsync` 仍可返回合同金额/账户余额字段供内部或对比面板使用，但 UI 指标卡与详情速览按规格裁剪。  
5. Fake 服务每加接口方法必须同步，否则 `CompanyPageTests` 编译失败。  
6. 查询注意 `AsNoTracking` 与权限 `EnsureAccessAsync`。  
7. 证书到期展示可复用现有证书页逻辑/样式类，避免重复造轮。

---

## Plan Self-Review

### Spec coverage

| 规格要点 | 任务 |
| --- | --- |
| 全部公司总览 + 下拉 | Task 4 |
| 2 指标卡 | Task 4 |
| Details + 6 tab + URL | Task 5–6 |
| 速览四指标无合同/余额 | Task 6 |
| profile/certificates 原位 | Task 6（迁移现有） |
| accounts 启停视觉与清默认 | Task 2 + 6 |
| projects/finance 只读+链接 | Task 3 + 6 + 7 |
| 按 tab 加载 | Task 5 |
| 权限 | 现有 Authorize + CanManage；Task 6 只读断言 |
| 验收标准 | Task 8 |

### Placeholder scan

无 TBD/TODO 实现步骤；测试与命令已写明。

### Type consistency

DTO 名称在 Task 1 定义，Task 3/5/6 沿用同一套：`CompanyProjectRowDto`、`CompanyCollectionRowDto` 等。

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-23-company-workspace-tabs.md`.

**两种执行方式：**

1. **Subagent-Driven（推荐）** — 每个 Task 开新子代理，任务间回顾  
2. **Inline Execution** — 本会话按 executing-plans 连续执行并设检查点  

你更想用哪一种？
