# 工资台账工作台与中央账本自动并入实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将工资台账改为施工班组式桌面单页工作台，并让有效工资批次以同源只读记录自动出现在中央账本。

**Architecture:** `/Payroll/Index` 统一负责列表、弹窗编辑状态和工资保存；编辑弹窗由 Razor 局部视图按需加载，页面脚本只负责窗口、标签、依赖字段和金额核对。中央账本查询服务直接投影有效 `PayrollBatch` 及其既有 `AccountTransaction`，通过新增查询 DTO 返回只读工资付款，不创建第二份财务记录。

**Tech Stack:** ASP.NET Core Razor Pages、EF Core、原生 JavaScript、现有 CSS 设计系统、xUnit、FluentAssertions、SQLite 测试夹具。

**Git note:** 项目 `AGENTS.md` 要求 Git 历史操作必须另行确认，因此本计划不执行提交、变基或远程操作。

---

## 文件结构

- `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`：工资工作台布局、列表、查看弹窗和编辑弹窗容器。
- `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml.cs`：筛选、弹窗加载、保存及编辑器数据装载。
- `src/EngineeringManager.Web/Pages/Payroll/_PayrollEditor.cshtml`：可按需返回并在验证失败时服务端重绘的编辑表单。
- `src/EngineeringManager.Web/Pages/Payroll/PayrollWorkspaceModels.cs`：工资页面专用输入、选项和编辑器视图模型。
- `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml`：兼容页，不再渲染独立编辑器。
- `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs`：把旧深链参数重定向到工资工作台。
- `src/EngineeringManager.Web/wwwroot/js/pages/payroll-workspace.js`：弹窗、按需加载、人员标签、依赖字段和实时核对。
- `src/EngineeringManager.Web/wwwroot/css/pages.css`：工资工作台桌面布局与弹窗样式。
- `src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`：增加只读工资付款 DTO 和查询结果字段。
- `src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`：按权限和筛选条件投影有效工资付款。
- `src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml`：中央账本工资付款区域、总额和来源跳转。
- `tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`：工作台标记、兼容入口和桌面结构测试。
- `tests/EngineeringManager.Tests/Web/PayrollEditPageModelTests.cs`：迁移为工资工作台页面模型往返测试。
- `tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`：工资付款查询、筛选、状态与去重测试。

## Task 1：锁定工资工作台页面契约

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/PayrollEditPageModelTests.cs`

- [ ] **Step 1: 添加工作台结构失败测试**

在 `PayrollDisbursementPageTests` 增加断言，要求工资首页包含以下契约：

```csharp
index.Should().Contain("data-payroll-workspace")
    .And.Contain("payroll-workspace-layout")
    .And.Contain("data-payroll-dialog-open=\"create\"")
    .And.Contain("data-payroll-dialog-open=\"details\"")
    .And.Contain("data-payroll-dialog-open=\"edit\"")
    .And.Contain("data-payroll-details-dialog")
    .And.Contain("data-payroll-editor-dialog")
    .And.Contain("~/js/pages/payroll-workspace.js")
    .And.NotContain("asp-page=\"/Payroll/Edit\">新建工资批次");
```

同时要求 `_PayrollEditor.cshtml` 包含员工/班组标签、核对指标和保存按钮，`Edit.cshtml.cs` 包含到 `/Payroll/Index` 的重定向。

- [ ] **Step 2: 将页面模型往返测试切换到 `IndexModel`**

保留现有两个关键场景：

```csharp
await model.LoadEditorAsync(CancellationToken.None);
model.Editor.EmployeeLines.Should().ContainSingle(item =>
    item.PaymentId == employeePaymentId && item.Selected && item.Amount == 3_000m);
model.Editor.CrewLines.Should().ContainSingle(item =>
    item.PaymentId == crewPaymentId && item.Selected && item.Amount == 4_000m);

var result = await model.OnPostSaveAsync(CancellationToken.None);
result.Should().BeOfType<RedirectToPageResult>();
```

第二个场景继续验证同一人员存在两个班组关系时，只选中历史工资所属班组。

- [ ] **Step 3: 运行测试确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PayrollDisbursementPageTests|FullyQualifiedName~PayrollEditPageModelTests" --no-restore
```

Expected: FAIL，缺少工资工作台结构、局部视图和 `IndexModel` 编辑接口。

## Task 2：建立工资单页工作台服务端结构

**Files:**
- Create: `src/EngineeringManager.Web/Pages/Payroll/PayrollWorkspaceModels.cs`
- Create: `src/EngineeringManager.Web/Pages/Payroll/_PayrollEditor.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs`

- [ ] **Step 1: 提取页面专用编辑模型**

新增以下页面级类型，不移动或改变应用层工资 DTO：

```csharp
public sealed record PayrollSelectOption(Guid Id, string Label);
public sealed record PayrollAccountOption(
    Guid Id,
    Guid LegalEntityId,
    string Label,
    FinancialAccountType Type,
    string? OwnerName,
    Guid? OwnerEmployeeId);

public sealed class PayrollEditorInput
{
    public string BatchNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly? PaymentDate { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? PersonalAdvanceAccountId { get; set; }
    public decimal ActualAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? VoucherNumber { get; set; }
    public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;
    public string? Notes { get; set; }
    public PayrollDisbursementType DisbursementType { get; set; } = PayrollDisbursementType.Wage;
    public PayrollFundingSource FundingSource { get; set; } = PayrollFundingSource.CompanyAccount;
    public Guid? RepaysPersonalAdvanceAccountId { get; set; }
    public Guid? ConcurrencyStamp { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<PayrollPersonLineInput> EmployeeLines { get; set; } = [];
    public List<PayrollPersonLineInput> CrewLines { get; set; } = [];
}
```

`PayrollPersonLineInput` 保留现有人员、班组、付款类别、工资性质、劳务公司、项目、金额和说明字段。`PayrollEditorViewModel` 组合 `EditorId`、`LineId`、`ReturnUrl`、输入模型和四组选项。

- [ ] **Step 2: 将编辑装载与保存移动到工资首页模型**

`IndexModel` 注入 `ApplicationDbContext`，增加：

```csharp
[BindProperty(SupportsGet = true)] public PayrollBatchStatus? Status { get; set; }
[BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
[BindProperty(SupportsGet = true)] public Guid? LineId { get; set; }
[BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
[BindProperty(SupportsGet = true)] public string? Dialog { get; set; }
[BindProperty] public PayrollEditorInput Editor { get; set; } = new();
public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator)
    || User.IsInRole(SystemRoles.ApplicationAdministrator)
    || User.IsInRole(SystemRoles.Finance);
```

从原 `EditModel` 移入并改名以下逻辑：

- `LoadEditorAsync`
- `ReloadPersonLinesAsync`
- `LoadOptionsAsync`
- `MakeLine`
- `OnPostSaveAsync`

保存成功时，无 `returnUrl` 则回到工资首页并以 `dialog=details&id=<batchId>` 打开查看弹窗；验证失败时重载工作台数据、保留 `ModelState`，设置 `Dialog = "editor"` 并返回 `Page()`。

- [ ] **Step 3: 新建编辑局部视图**

把原编辑页表单迁入 `_PayrollEditor.cshtml`，并调整为：

```html
<form method="post" asp-page-handler="Save" data-payroll-editor data-payroll-dependent-fields>
  <input type="hidden" name="Id" value="@Model.EditorId" />
  <input type="hidden" name="LineId" value="@Model.LineId" />
  <input type="hidden" name="ReturnUrl" value="@Model.ReturnUrl" />
  <input type="hidden" asp-for="Input.ConcurrencyStamp" />
  <div class="payroll-editor-sections">
    <section><h3>批次资料</h3><label>批次编号<input asp-for="Input.BatchNumber" required /></label><label>批次名称<input asp-for="Input.Name" required /></label><label>发放日期<input asp-for="Input.PaymentDate" type="date" /></label><label>发放类型<select asp-for="Input.DisbursementType"></select></label></section>
    <section><h3>付款信息</h3><label>发放项目<select asp-for="Input.ProjectId"></select></label><label>发放公司<select asp-for="Input.LegalEntityId"></select></label><label>资金来源<select asp-for="Input.FundingSource" data-payroll-funding-source></select></label><label>实际发放总金额<input asp-for="Input.ActualAmount" type="number" step="0.01" data-payroll-actual /></label></section>
  </div>
  <div class="payroll-editor-tabs" role="tablist">
    <button type="button" role="tab" data-payroll-editor-tab="employees">自有员工</button>
    <button type="button" role="tab" data-payroll-editor-tab="crews">施工班组人员</button>
  </div>
  <section data-payroll-editor-panel="employees"><table class="data-table payroll-line-table"><thead><tr><th>选择</th><th>员工</th><th>付款类别</th><th>工资性质</th><th>劳务公司</th><th>项目</th><th>实际金额</th><th>说明</th></tr></thead><tbody data-payroll-employee-lines></tbody></table></section>
  <section data-payroll-editor-panel="crews" hidden><table class="data-table payroll-line-table"><thead><tr><th>选择</th><th>劳务公司</th><th>人员</th><th>付款类别</th><th>工资性质</th><th>项目</th><th>实际金额</th><th>说明</th></tr></thead><tbody data-payroll-crew-lines></tbody></table></section>
  <section class="payroll-reconciliation"><div><span>人员明细合计</span><strong data-payroll-detail-total>0.00</strong></div><div><span>实际发放总额</span><strong data-payroll-actual-total>0.00</strong></div><div><span>批次差额</span><strong data-payroll-difference>0.00</strong></div></section>
  <div class="quick-edit-actions"><button type="button" class="button button--secondary" data-payroll-dialog-close>取消</button><button type="submit" class="button button--primary">保存工资批次</button></div>
</form>
```

所有输入名称继续以 `Editor.` 为前缀，确保 `OnPostSaveAsync` 模型绑定完整。

- [ ] **Step 4: 重建工资首页工作台**

将首页结构改为：

```html
<div class="payroll-workspace-page" data-payroll-workspace data-active-dialog="@Model.Dialog">
  <section class="page-heading compact-page-heading payroll-workspace-heading"><div><p class="eyebrow">真实付款批次</p><h1>工资台账</h1><p class="page-lead">统一维护工资发放与人员明细</p></div><button class="button button--primary" type="button" data-payroll-dialog-open="create">新建工资批次</button></section>
  <section class="payroll-workspace-layout">
    <aside class="payroll-workspace-summary"><div class="payroll-summary-metrics"><article><span>批次数</span><strong>@Model.Overview.Batches.Count</strong></article><article><span>实际发放</span><strong>@Model.Overview.ActualAmount.ToString("N2")</strong></article><article><span>员工发放</span><strong>@Model.Overview.EmployeeAmount.ToString("N2")</strong></article><article><span>班组发放</span><strong>@Model.Overview.CrewAmount.ToString("N2")</strong></article></div></aside>
    <section class="payroll-workspace-list"><div class="equipment-list-toolbar equipment-list-toolbar--integrated payroll-list-toolbar"><div><p class="eyebrow">批次记录</p><h2>真实工资发放</h2></div><partial name="_DataWorkbench" model="workbench" /></div><div class="table-wrap payroll-table-wrap"><table class="data-table payroll-workspace-table" id="payroll-disbursement-table"></table></div></section>
  </section>
  <dialog class="workbench-dialog payroll-details-dialog mac-window-dialog" data-payroll-details-dialog><div class="workbench-dialog-heading"><strong data-payroll-detail-title>工资批次详情</strong><button type="button" class="dialog-close" data-payroll-dialog-close>×</button></div><div class="payroll-details-grid" data-payroll-details-content></div></dialog>
  <dialog class="workbench-dialog payroll-editor-dialog mac-window-dialog" data-payroll-editor-dialog><div class="workbench-dialog-heading"><strong data-payroll-editor-title>工资批次</strong><button type="button" class="dialog-close" data-payroll-dialog-close>×</button></div><div data-payroll-editor-content></div></dialog>
</div>
```

列表行生成只读详情 JSON；“新建”加载空编辑器，“查看”打开详情，“编辑”按需请求 `?handler=Editor&id=<id>&lineId=<lineId>`。

- [ ] **Step 5: 将旧编辑页改为兼容重定向**

`EditModel.OnGet` 返回：

```csharp
return RedirectToPage("/Payroll/Index", new
{
    id = Id,
    lineId = LineId,
    returnUrl = ReturnUrl,
    dialog = "editor"
});
```

`Edit.cshtml` 只保留 `@page` 和模型声明，不再重复表单。

- [ ] **Step 6: 运行工资页面模型测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PayrollDisbursementPageTests|FullyQualifiedName~PayrollEditPageModelTests" --no-restore
```

Expected: PASS。

## Task 3：实现施工班组式弹窗交互与桌面样式

**Files:**
- Create: `src/EngineeringManager.Web/wwwroot/js/pages/payroll-workspace.js`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`

- [ ] **Step 1: 增加脚本与样式失败断言**

断言脚本具备以下行为标记：

```csharp
script.Should().Contain("[data-payroll-workspace]")
    .And.Contain("[data-payroll-dialog-open]")
    .And.Contain("fetch(")
    .And.Contain("data-payroll-editor-tab")
    .And.Contain("data-payroll-detail-total")
    .And.Contain("dialog.close()");

css.Should().Contain(".payroll-workspace-layout")
    .And.Contain(".payroll-workspace-summary")
    .And.Contain(".payroll-workspace-table")
    .And.Contain(".payroll-editor-dialog")
    .And.Contain(".payroll-editor-tabs");
```

- [ ] **Step 2: 实现工资工作台脚本**

脚本复用施工班组页面的窗口行为，并提供以下明确流程：

```javascript
const show = (dialog) => {
  if (!dialog?.open) dialog?.showModal();
};

const updateReconciliation = (root) => {
  let detail = 0;
  root.querySelectorAll("[data-payroll-amount]").forEach((input) => {
    const selected = input.closest("tr")?.querySelector("input[type='checkbox']");
    if (selected?.checked) detail += Number(input.value || 0);
  });
  const actual = Number(root.querySelector("[data-payroll-actual]")?.value || 0);
  root.querySelector("[data-payroll-detail-total]").textContent = detail.toFixed(2);
  root.querySelector("[data-payroll-actual-total]").textContent = actual.toFixed(2);
  root.querySelector("[data-payroll-difference]").textContent = (actual - detail).toFixed(2);
};
```

编辑器加载后初始化：资金来源字段、公司账户过滤、人员标签切换、`lineId` 高亮、输入与复选框监听。请求失败时在弹窗内显示错误，不跳离工作台。

- [ ] **Step 3: 添加桌面工作台样式**

以施工班组样式为基础设置两栏布局、固定摘要宽度、表格最小宽度、操作按钮组和窗口尺寸。编辑弹窗使用受视口约束的宽高与内部滚动：

```css
.payroll-workspace-layout {
  display: grid;
  grid-template-columns: minmax(13rem, 16rem) minmax(0, 1fr);
  gap: var(--space-3);
  align-items: start;
}

.payroll-editor-dialog {
  width: min(92rem, calc(100vw - 3rem));
  max-height: calc(100vh - 3rem);
}
```

不添加新的手机端媒体查询；已有全局保护规则可保留，但本轮不验证移动端。

- [ ] **Step 4: 运行页面资产测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PayrollDisbursementPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore
```

Expected: PASS。

## Task 4：让中央账本同源查询有效工资付款

**Files:**
- Modify: `src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`
- Modify: `tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`

- [ ] **Step 1: 添加中央账本工资查询失败测试**

测试夹具写入四个工资批次：`Draft`、`Confirmed`、`Closed`、`Voided`，有效批次关联现有 `PayrollPayment` 与 `AccountTransactionSourceType.PayrollPayment` 流水。断言：

```csharp
var result = await query.SearchAsync(
    fixture.ExternalActor(),
    new CentralLedgerQuery(LedgerScope.External),
    CancellationToken.None);

result.PayrollPayments.Should().HaveCount(2);
result.PayrollPaymentTotal.Should().Be(confirmed.ActualAmount + closed.ActualAmount);
result.PayrollPayments.Should().OnlyContain(item =>
    item.Status is PayrollBatchStatus.Confirmed or PayrollBatchStatus.Closed);
```

补充测试覆盖公司、项目、日期、财务年度、关键词、应收方向以及未授权项目筛选；断言查询不会新增 `AccountTransaction` 或 `FinanceCashEntry`。

- [ ] **Step 2: 运行中央账本测试确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CentralLedgerQueryServiceTests" --no-restore
```

Expected: FAIL，`CentralLedgerOverviewPageDto` 尚无工资付款结果。

- [ ] **Step 3: 扩展中央账本查询 DTO**

新增：

```csharp
public sealed record CentralLedgerPayrollPaymentDto(
    Guid BatchId,
    string BatchNumber,
    string BatchName,
    DateOnly PaymentDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid AccountId,
    string AccountName,
    decimal EmployeeAmount,
    decimal CrewAmount,
    decimal ActualAmount,
    PayrollBatchStatus Status);
```

在 `CentralLedgerOverviewPageDto` 末尾增加可选 `PayrollPayments` 和计算或显式返回的 `PayrollPaymentTotal`，保持现有调用点兼容。

- [ ] **Step 4: 实现同源工资付款查询**

在 `CentralLedgerQueryService.SearchAsync` 中调用私有 `SearchPayrollPaymentsAsync`。查询条件必须包含：

```csharp
item.IsUnifiedDisbursement
&& item.AccountTransactionId.HasValue
&& (item.Status == PayrollBatchStatus.Confirmed
    || item.Status == PayrollBatchStatus.Closed
    || item.Status == PayrollBatchStatus.ModifiedPendingReview)
&& legalEntityIds.Contains(item.LegalEntityId!.Value)
&& (!item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value))
```

当范围为内部账本或方向为应收时返回空集合。继续应用日期、财务年度、公司、项目、合作单位、合同和关键词筛选；合作单位与合同通过班组人员及 `PayrollCrewAllocation` 关联匹配。结果金额从同一批次人员明细汇总，实际金额使用 `PayrollBatch.ActualAmount`。

- [ ] **Step 5: 运行中央账本查询测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CentralLedgerQueryServiceTests|FullyQualifiedName~PayrollDisbursementFinanceTests" --no-restore
```

Expected: PASS，且测试库中账户流水数量未增加。

## Task 5：在中央账本展示只读工资付款

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml`
- Modify: `tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`

- [ ] **Step 1: 添加页面失败测试**

断言外部账本包含：

```csharp
page.Should().Contain("id=\"payroll-payments\"")
    .And.Contain("工资付款")
    .And.Contain("Model.Result.PayrollPaymentTotal")
    .And.Contain("asp-page=\"/Payroll/Index\"")
    .And.Contain("asp-route-dialog=\"details\"")
    .And.NotContain("asp-page-handler=\"EditPayroll\"");
```

- [ ] **Step 2: 添加工资付款只读区域**

在外部账本付款区域展示：日期、批次、公司、项目、账户、员工、班组、实际总额、状态和“查看工资批次”。顶部显示独立工资付款总额，不修改 `_LedgerMetrics` 的结算金额口径。

来源链接使用：

```html
<a asp-page="/Payroll/Index"
   asp-route-id="@payment.BatchId"
   asp-route-dialog="details">查看工资批次</a>
```

- [ ] **Step 3: 运行中央账本页面测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CentralLedgerPageTests|FullyQualifiedName~CentralLedgerQueryServiceTests" --no-restore
```

Expected: PASS。

## Task 6：综合验证与桌面端验收

**Files:**
- Modify if required by failures: files listed above only

- [ ] **Step 1: 运行定向工资与中央账本测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~Payroll|FullyQualifiedName~CentralLedger|FullyQualifiedName~ConstructionCrewPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore
```

Expected: PASS，0 failed。

- [ ] **Step 2: 运行 Release 构建**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager.sln -c Release --no-restore
```

Expected: Build succeeded，0 errors。

- [ ] **Step 3: 启动本地应用进行桌面验证**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet run --project src/EngineeringManager.Web/EngineeringManager.Web.csproj --no-build
```

使用可用的桌面浏览器控制能力验证：

- 工资工作台两栏布局、表格和操作按钮不重叠。
- 新建、查看、编辑弹窗能打开和关闭。
- 员工/班组标签、资金来源依赖字段和金额差额更新正确。
- 验证失败后弹窗保持打开且输入不丢失。
- 中央账本工资付款记录与工资批次金额一致，来源链接返回对应弹窗。
- 控制台无错误，关键请求无 4xx/5xx。

按用户要求，不执行手机端视口验证。

- [ ] **Step 4: 检查最终差异**

Run:

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: `git diff --check` 无输出；只包含本任务相关文件和用户原有未跟踪文件，不提交 Git。
