# Partner Financial Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在合作单位工作台主档表格中，为授权用户一次性展示每个单位的收款、开票和付款汇总，并把桌面端左侧概览压缩到约 220px。

**Architecture:** 中央账本查询服务新增按合作单位 ID 集合批量汇总接口，在授权公司和项目范围内一次加载有效外部结算，并复用 `ToRow` 与 `CentralLedgerCalculator.Add` 分别累计应收和应付指标。合作单位页面模型仅在具备财务查看权限时创建中央账本 Actor、调用一次批量接口并保存字典；Razor 根据权限动态渲染三列和空状态 `colspan`。

**Tech Stack:** ASP.NET Core Razor Pages、Entity Framework Core、现有中央账本领域计算器、xUnit、FluentAssertions、原生 CSS。

---

### Task 1: 锁定中央账本批量汇总契约

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/ICentralLedgerQueryService.cs`

- [x] **Step 1: 写失败测试，覆盖分方向聚合、跨单位隔离和空单位**

在 `CentralLedgerQueryServiceTests` 增加测试：为 `Client` 创建应收结算、为 `Supplier` 创建应付结算，并分别产生发票及现金分摊；随后一次传入 `Client.Id`、`Supplier.Id` 和一个无记录 ID。

```csharp
var summaries = await query.GetPartnerSummariesAsync(
    fixture.ExternalActor(),
    [fixture.Client.Id, fixture.Supplier.Id, emptyPartnerId],
    CancellationToken.None);

summaries[fixture.Client.Id].Receivable.CashAmount.Should().Be(expectedReceived);
summaries[fixture.Client.Id].Receivable.UncollectedOrUnpaid.Should().Be(expectedUncollected);
summaries[fixture.Client.Id].Receivable.InvoicedAmount.Should().Be(expectedSalesInvoice);
summaries[fixture.Client.Id].Payable.Should().Be(CentralLedgerMetrics.Zero);
summaries[fixture.Supplier.Id].Payable.CashAmount.Should().Be(expectedPaid);
summaries[fixture.Supplier.Id].Payable.UncollectedOrUnpaid.Should().Be(expectedUnpaid);
summaries[fixture.Supplier.Id].Payable.InvoicedAmount.Should().Be(expectedPurchaseInvoice);
summaries[emptyPartnerId].Should().Be(PartnerLedgerSummaryDto.Empty(emptyPartnerId));
```

- [x] **Step 2: 运行测试并确认接口缺失导致红灯**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CentralLedgerQueryServiceTests" --no-restore
```

Expected: FAIL，提示 `GetPartnerSummariesAsync` 或 `PartnerLedgerSummaryDto` 尚不存在。

- [x] **Step 3: 增加双向汇总 DTO 与批量接口签名**

```csharp
public sealed record PartnerLedgerSummaryDto(
    Guid BusinessPartnerId,
    CentralLedgerMetrics Receivable,
    CentralLedgerMetrics Payable)
{
    public static PartnerLedgerSummaryDto Empty(Guid businessPartnerId) =>
        new(businessPartnerId, CentralLedgerMetrics.Zero, CentralLedgerMetrics.Zero);
}
```

```csharp
Task<IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto>> GetPartnerSummariesAsync(
    CentralLedgerActor actor,
    IReadOnlyCollection<Guid> businessPartnerIds,
    CancellationToken token);
```

### Task 2: 实现授权范围内的批量聚合

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`

- [x] **Step 1: 一次加载目标合作单位的有效外部结算**

实现 `GetPartnerSummariesAsync`：先对 ID 去重；空集合直接返回空字典。查询条件包含 `LedgerScope.External`、`LedgerRecordStatus.Active`、Actor 的公司范围、Actor 的项目范围以及目标合作单位集合，并加载 `ToRow` 所需的调整、扣款、发票分摊和现金分摊。

```csharp
var settlements = await db.FinanceSettlements.AsNoTracking().AsSplitQuery()
    .Where(item => item.Scope == LedgerScope.External && item.Status == LedgerRecordStatus.Active)
    .Where(item => item.BusinessPartnerId.HasValue && partnerIds.Contains(item.BusinessPartnerId.Value))
    .Where(item => legalEntityIds.Contains(item.LegalEntityId))
    .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value))
    .Include(item => item.Adjustments)
    .Include(item => item.Deductions)
    .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
    .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
    .ToListAsync(token);
```

- [x] **Step 2: 按合作单位与方向复用领域计算器累计**

先为每个请求 ID 初始化 `PartnerLedgerSummaryDto.Empty(id)`，遍历 `settlements.Select(ToRow)`，应收方向累计到 `Receivable`，应付方向累计到 `Payable`，每次使用 `CentralLedgerCalculator.Add`，不循环调用 `GetPartnerMetricsAsync`。

- [x] **Step 3: 运行中央账本定向测试并确认绿灯**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CentralLedgerQueryServiceTests" --no-restore
```

Expected: PASS，旧的 `GetPartnerMetricsAsync` 测试继续通过。

### Task 3: 锁定页面权限与渲染契约

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerStageResultAuthorizationTests.cs`

- [x] **Step 1: 增加页面源码契约失败测试**

要求页面模型包含 `CanViewFinance`、`PartnerFinancialSummaries` 和单次 `GetPartnerSummariesAsync` 调用；页面包含 `receipts`、`invoices`、`payments` 三个列键、两行金额标签、动态 `financialColumnCount` 与 `colspan`；预设包含三列；CSS 包含 `220px` 左列与财务单元格样式。

- [x] **Step 2: 增加角色集成失败测试**

更新测试工厂，注册可记录调用次数的 `ICentralLedgerQueryService` 假实现。断言系统管理员、应用管理员、财务、查询角色能看到 `data-partner-financial-summary`，项目经理看不到财务汇总列且假服务调用次数为零；原有“查询角色无财务操作按钮”的断言保持不变。

- [x] **Step 3: 运行页面定向测试并确认红灯**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PartnerWorkspacePageTests|FullyQualifiedName~PartnerStageResultAuthorizationTests" --no-restore
```

Expected: FAIL，缺少财务汇总列、权限属性或批量服务调用。

### Task 4: 在页面模型中按权限加载财务汇总

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs`

- [x] **Step 1: 注入中央账本服务和数据库上下文**

页面模型构造函数增加 `ICentralLedgerQueryService ledgerQueryService` 与 `ApplicationDbContext db`，并引入 `EngineeringManager.Application.Finance`、`EngineeringManager.Web.Pages.Ledger`。

- [x] **Step 2: 增加只读权限与汇总字典**

```csharp
public bool CanViewFinance =>
    User.IsInRole(SystemRoles.SystemAdministrator) ||
    User.IsInRole(SystemRoles.ApplicationAdministrator) ||
    User.IsInRole(SystemRoles.Finance) ||
    User.IsInRole(SystemRoles.QueryOnly);

public IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries { get; private set; }
    = new Dictionary<Guid, PartnerLedgerSummaryDto>();
```

- [x] **Step 3: 在 `LoadAsync` 完成列表过滤后调用一次批量接口**

仅当 `CanViewFinance` 且当前 `Partners` 非空时，通过 `LedgerPageSupport.CreateActorAsync(User, db, cancellationToken)` 创建 Actor，并传入当前页面所有合作单位 ID。非授权角色不创建 Actor、不访问中央账本服务。

### Task 5: 渲染财务列并调整工作台预设

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`

- [x] **Step 1: 动态输出三列和空状态列数**

定义 `financialColumnCount = Model.CanViewFinance ? 3 : 0`，表头在“参与项目”后按权限输出“收款”“开票”“付款”，空状态使用 `colspan="@(6 + financialColumnCount)"`。

- [x] **Step 2: 每个合作单位输出统一两行金额**

从字典读取 DTO，缺失时使用 `PartnerLedgerSummaryDto.Empty(partner.Id)`。金额使用 `N2`，内容如下：

```html
<td data-column-key="receipts" data-partner-financial-summary>
  <span><small>已收</small><strong>@summary.Receivable.CashAmount.ToString("N2")</strong></span>
  <span><small>未收</small><strong>@summary.Receivable.UncollectedOrUnpaid.ToString("N2")</strong></span>
</td>
<td data-column-key="invoices">
  <span><small>销项</small><strong>@summary.Receivable.InvoicedAmount.ToString("N2")</strong></span>
  <span><small>进项</small><strong>@summary.Payable.InvoicedAmount.ToString("N2")</strong></span>
</td>
<td data-column-key="payments">
  <span><small>已付</small><strong>@summary.Payable.CashAmount.ToString("N2")</strong></span>
  <span><small>未付</small><strong>@summary.Payable.UncollectedOrUnpaid.ToString("N2")</strong></span>
</td>
```

- [x] **Step 3: 预设增加 `receipts`、`invoices`、`payments`**

保持原有列键顺序，在 `projects` 和 `status` 之间加入三个财务列，列管理与实际表头一致。

### Task 6: 压缩左栏并稳定金额排版

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`

- [x] **Step 1: 将桌面左栏改为 220px**

```css
.partner-workspace-layout { grid-template-columns: minmax(210px, 220px) minmax(0, 1fr); }
```

保持现有 `@media (max-width: 900px)` 的 `grid-template-columns: 1fr`，移动端继续上下排列。

- [x] **Step 2: 增加稳定的两行金额样式并重新分配列宽**

财务单元格使用网格、右对齐、等宽数字和不换行；列宽改用 `data-column-key` 选择器，避免授权与非授权角色的动态列造成 `nth-child` 偏移。单位名称继续两行截断，操作按钮继续单行。

- [x] **Step 3: 运行页面定向测试并确认绿灯**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~PartnerWorkspacePageTests|FullyQualifiedName~PartnerStageResultAuthorizationTests" --no-restore
```

Expected: PASS。

### Task 7: 完整验证与本地服务更新

**Files:**
- Verify only; no planned production file additions.

- [x] **Step 1: 运行完整测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager.sln --no-restore
```

Expected: 全部测试通过。

- [x] **Step 2: 运行 Release 构建和差异检查**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build EngineeringManager.sln -c Release --no-restore
git diff --check
```

Expected: 0 errors，`git diff --check` 无输出。

- [x] **Step 3: 重启 5091 的本地 Release 服务并验证 HTTP**

重新查询监听 5091 的 PID，只停止该精确进程；从 `EngineeringManager/src/EngineeringManager.Web` 目录以 `ASPNETCORE_ENVIRONMENT=Development` 启动 Release 程序。请求 `/Partners`，确认服务可访问；不迁移数据库，不提交或推送 Git。
