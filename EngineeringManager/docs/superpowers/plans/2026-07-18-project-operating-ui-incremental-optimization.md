# 项目经营展示增量优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Do not start implementation until the user gives an explicit command. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不重构现有业务模型的前提下，统一首页与项目详情的现有经营汇总口径，突出项目阶段、结算/开票/回款进度和项目现金，并为现有并发错误补充明确弹窗。

**Architecture:** 继续使用现有 `ProjectSummaryDto`、`FinanceProjectSummaryDto`、`FinanceLedgerService`、`DashboardService` 和 `ProjectWorkspaceDto`。只增加受项目权限约束的财务汇总复用入口、少量展示 DTO/纯计算辅助和 Razor/CSS/JS 调整；不新增业务表、不复制财务数据、不改变现有模块导航。

**Tech Stack:** ASP.NET Core Razor Pages、Entity Framework Core、C#、原生 JavaScript、现有 CSS 组件、SQLite 测试、SQL Server 测试库、xUnit、FluentAssertions、PowerShell 7。

**Execution gate:** 当前“临时人员并入员工管理”任务完成、全量测试和质量门禁通过后，才能开始本计划。若“统一财务账本”在本计划之前进入实施，先重新核对 Task 1 和 Task 3 的读取接口，只保留 UI 任务，禁止同时改两套财务汇总契约。

**Git boundary:** 本计划不授权暂存、提交、推送、创建分支或改写 Git 历史。所有 Git 历史操作等待用户单独确认。

---

## 1. 当前任务与去重结论

### 1.1 已完成且本计划不重复实现

当前工作区已经完成并验证以下项目能力：

- 项目阶段收敛为现有五项，并删除独立归档状态。
- 独立合同签订状态。
- 项目税率与普票/专票组合配置。
- 发票公司、项目税金组合和项目归属校验。
- 重要施工机械在项目总览显示、跳转和定位。
- 项目备注布局、原位编辑和项目级公司/税金选项过滤。
- 桌面与 390px 项目页面响应式验收。

本计划不再修改以上模型、迁移、页面控件或校验规则。

### 1.2 正在执行且本计划不触碰

“临时人员并入员工管理”任务将完成：

- `EmployeeType.Temporary` 特殊临时员工类型。
- 工资发放中的直接人员统一为 `Employee`，班组人员继续使用 `CrewWorker`。
- 历史临时人员、工资付款和项目归属迁移。
- `PersonnelMigrationMap` 旧 ID 映射和旧 URL 跳转。
- 员工年度总账连续性。
- 临时人员的导入、导出、样例数据、权限和导航合并。
- 删除旧临时人员运行时服务、表和专用工资字段。
- SQL Server 测试库迁移对账、全量测试和质量门禁。

本计划不修改员工、工资、临时人员、班组工资、导入导出、权限、样例数据和相关数据库迁移。

### 1.3 已有独立规格且本计划不提前实现

`2026-07-18-unified-finance-reconciliation-design.md` 已负责：

- 中央结算单、发票、资金单和分摊。
- 自有公司、项目、班组、合作商的财务单一数据源。
- 待分摊、跨项目分摊、内部往来和集团抵销。
- 累计总账、年度账、阶段对账点和冻结结转。
- 全局财务核对中心和完整数据迁移。

本计划不新增中央财务实体、不改现有财务表结构、不实现分摊/年度账/对账点，也不建设另一套数据一致性中心。

### 1.4 本计划最终保留范围

- 首页财务数字复用现有 `FinanceLedgerService` 口径。
- 项目详情把现有数字重排为结算、开票、回款和项目现金。
- 首页增加轻量“项目现金关注”列表，完整查询继续复用现有财务页。
- 现有并发错误增加通用、可关闭、可刷新的弹出提示。
- 补齐定向测试、性能回归和真实浏览器验收。
- 不包含批量处理，不新增施工进度百分比，不重做导航。

---

## 2. 文件边界

### Application

- Modify `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`: 增加按授权项目 ID 集合读取现有项目财务汇总的重载。
- Modify `src/EngineeringManager.Application/Dashboard/DashboardDtos.cs`: 增加首页现金合计和项目现金行 DTO，保留现有构造兼容。

### Infrastructure

- Modify `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`: 让全部项目与指定项目集合共用现有汇总实现。
- Modify `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`: 删除页面专用的财务加减口径，改为消费现有财务服务结果；继续自行处理项目阶段、设备、工资和提醒。

### Web

- Create `src/EngineeringManager.Web/Presentation/ProjectOperatingDisplay.cs`: 只把现有项目/财务 DTO 组合成展示指标，不读数据库、不保存数据。
- Modify `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`: 重排经营摘要和三项进度，不改变现有标签页与编辑入口。
- Modify `src/EngineeringManager.Web/Pages/Index.cshtml`: 重排首页指标、三项进度并增加项目现金关注列表。
- Modify `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`: 增加全局冲突提示容器。
- Create `src/EngineeringManager.Web/wwwroot/js/components/conflict-notice.js`: 从现有验证消息识别并发冲突并显示弹窗。
- Modify `src/EngineeringManager.Web/wwwroot/js/site.js`: 初始化冲突提示组件。
- Modify `src/EngineeringManager.Web/wwwroot/css/components.css`: 冲突提示样式。
- Modify `src/EngineeringManager.Web/wwwroot/css/pages.css`: 项目经营摘要、进度区和首页现金表布局。

### Tests

- Modify `tests/EngineeringManager.Tests/Application/FinanceSummaryTests.cs`.
- Modify `tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`.
- Create `tests/EngineeringManager.Tests/Web/ProjectOperatingDisplayTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/HomePageTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`.
- Modify `tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`.

### Explicitly untouched

- `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`.
- `src/EngineeringManager.Infrastructure/Data/Migrations/`.
- 所有 `TemporaryWorkers`、`Payroll`、`Employees` 生产文件。
- 中央财务账本、分摊、年度账和阶段对账相关新实体。

---

## Task 0: 等待当前任务完成并建立新基线

**Files:** Read-only inspection only.

- [ ] **Step 1: 确认当前执行线程完成。**

读取“临时人员并入员工管理”任务的最终状态，必须明确显示 Task 1-8 已完成、迁移仅作用于测试库、全量测试通过、质量门禁通过。若线程仍在 Task 2 或工作区仍是不可编译中间态，本计划停止，不修改任何应用文件。

- [ ] **Step 2: 检查工作区边界。**

Run from `D:\AI\TZM-project`:

```powershell
$ErrorActionPreference = 'Stop'
git status --short --branch
git diff --name-only
```

Expected: 能明确区分前序任务改动与本计划即将触及的文件；不得回退或覆盖任何已有改动。

- [ ] **Step 3: 运行前序基线。**

Run from `D:\AI\TZM-project\EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release
```

Expected: 全量 PASS。若失败，先报告为前序任务基线问题，不在本计划中混入修复。

- [ ] **Step 4: 复核统一财务任务状态。**

若中央财务账本已经开始改 `IFinanceLedgerService`、`FinanceDtos` 或财务实体，则暂停 Task 1 和 Task 3，先按新接口重写本计划对应步骤；项目详情展示和冲突提示仍可独立执行。

---

## Task 1: 让首页复用现有项目财务汇总口径

**Files:**

- Modify `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Modify `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- Test `tests/EngineeringManager.Tests/Application/FinanceSummaryTests.cs`
- Test `tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`

- [ ] **Step 1: 写指定项目范围汇总的失败测试。**

在 `FinanceSummaryTests` 增加测试，准备两个项目，只把第一个项目 ID 传入服务，并断言返回结果不包含第二个项目：

```csharp
[Fact]
public async Task OverviewCanBeRestrictedToAuthorizedProjectIds()
{
    await using var fixture = await FinanceSummaryFixture.CreateAsync();
    var otherProject = new Project
    {
        ProjectNumber = "FIN-SUM-OTHER",
        Name = "其他项目",
        Stage = ProjectStage.UnderConstruction
    };
    fixture.Db.Projects.Add(otherProject);
    await fixture.Db.SaveChangesAsync();

    var overview = await fixture.Service.GetOverviewAsync(
        new[] { fixture.Project.Id },
        CancellationToken.None);

    overview.Projects.Should().ContainSingle(item => item.ProjectId == fixture.Project.Id);
    overview.Projects.Should().NotContain(item => item.ProjectId == otherProject.Id);
    var projectSummary = overview.Projects.Single().Summary;
    overview.Total.ReceivableAmount.Should().Be(projectSummary.ReceivableAmount);
    overview.Total.CollectedAmount.Should().Be(projectSummary.CollectedAmount);
    overview.Total.UncollectedAmount.Should().Be(projectSummary.UncollectedAmount);
    overview.Total.PayableAmount.Should().Be(projectSummary.PayableAmount);
    overview.Total.PaidAmount.Should().Be(projectSummary.PaidAmount);
    overview.Total.UnpaidAmount.Should().Be(projectSummary.UnpaidAmount);
    overview.Total.OutputInvoiceAmount.Should().Be(projectSummary.OutputInvoiceAmount);
}
```

- [ ] **Step 2: 写首页与财务服务口径一致的失败测试。**

扩展 `DashboardServiceTests` 的样例，包含退款、付款冲销、扣款和班组工资代发；分别调用 `DashboardService` 与 `FinanceLedgerService.GetOverviewAsync(projectIds)`，断言已收、已付、待收和待付一致。测试必须先因首页仍使用独立查询逻辑而失败。

- [ ] **Step 3: 运行 RED。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~FinanceSummaryTests|FullyQualifiedName~DashboardServiceTests"
```

Expected: FAIL，原因是指定项目 ID 的服务重载尚不存在，或首页口径仍与财务服务不一致。

- [ ] **Step 4: 增加最小服务重载。**

在 `IFinanceLedgerService` 增加：

```csharp
Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(
    IReadOnlyCollection<Guid> projectIds,
    CancellationToken cancellationToken);

Task<FinanceOverviewDto> GetOverviewAsync(
    IReadOnlyCollection<Guid> projectIds,
    CancellationToken cancellationToken);
```

现有无参数项目范围的方法继续保留，并委托给同一私有查询实现。指定 ID 为空时返回空项目集合和全零 `FinanceProjectSummaryDto`，不得回退为“全部项目”。

- [ ] **Step 5: 修改首页服务依赖。**

将构造函数改为：

```csharp
public sealed class DashboardService(
    ApplicationDbContext db,
    IFinanceLedgerService financeService) : IDashboardService
```

首页先按现有权限得到 `projectIds`，再调用：

```csharp
var financeOverview = actor.CanViewFinance
    ? await financeService.GetOverviewAsync(projectIds, cancellationToken)
    : new FinanceOverviewDto([], EmptyFinanceSummary());
```

在 `DashboardService` 内增加完整的零值辅助，避免匿名或无财务权限分支访问数据库：

```csharp
private static FinanceProjectSummaryDto EmptyFinanceSummary() => new(
    Guid.Empty,
    0m,
    0m,
    0m,
    0m,
    0m,
    0m,
    0m,
    0m,
    0m,
    0m,
    false,
    false);
```

收款、付款、开票、退款、冲销、扣款和班组工资代发不再由 `DashboardService` 重新查询和加减。保留月度趋势、设备、工资摘要和提醒现有逻辑。

- [ ] **Step 6: 更新测试夹具并运行 GREEN。**

测试中的服务实例统一使用：

```csharp
var financeService = new FinanceLedgerService(db);
var dashboardService = new DashboardService(db, financeService);
```

重复 Step 3。Expected: PASS。

---

## Task 2: 在现有项目详情上重排经营摘要

**Files:**

- Create `src/EngineeringManager.Web/Presentation/ProjectOperatingDisplay.cs`
- Modify `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create `tests/EngineeringManager.Tests/Web/ProjectOperatingDisplayTests.cs`
- Modify `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify `tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] **Step 1: 写纯展示计算失败测试。**

新增以下测试，固定已确认口径：

```csharp
[Fact]
public void DisplayUsesSettlementInvoiceCollectionAndCashGapRules()
{
    var project = new ProjectSummaryDto(1_000m, 1_000m, 400m, 1_000m,
        ProjectSettlementStatus.PartiallySettled, 1, 1);
    var finance = new FinanceProjectSummaryDto(Guid.NewGuid(),
        900m, 500m, 400m,
        700m, 250m, 50m, 400m,
        300m, 600m, 0m,
        false, false);

    var result = ProjectOperatingDisplay.Create(project, finance);

    result.Settlement.Percentage.Should().Be(40m);
    result.Invoice.Percentage.Should().BeApproximately(33.33m, 0.01m);
    result.Collection.Percentage.Should().BeApproximately(55.56m, 0.01m);
    result.CashGap.Should().Be(0m);
    result.PendingCashNet.Should().Be(0m);
}
```

再增加 `UnpaidAmount=650m`、`UncollectedAmount=400m` 的用例，断言 `CashGap=250m`、`PendingCashNet=-250m`。

- [ ] **Step 2: 运行 RED。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectOperatingDisplayTests
```

Expected: FAIL，因为展示辅助类型尚不存在。

- [ ] **Step 3: 实现无状态展示辅助。**

创建：

```csharp
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;

namespace EngineeringManager.Web.Presentation;

public sealed record OperatingProgressDisplay(
    string Key,
    string Label,
    decimal TotalAmount,
    decimal CompletedAmount,
    decimal RemainingAmount,
    decimal Percentage);

public sealed record ProjectOperatingDisplay(
    decimal CurrentAmount,
    decimal SettledAmount,
    decimal InvoicedAmount,
    decimal CollectedAmount,
    decimal PaidAmount,
    decimal UncollectedAmount,
    decimal UnpaidAmount,
    decimal CashGap,
    decimal PendingCashNet,
    OperatingProgressDisplay Settlement,
    OperatingProgressDisplay Invoice,
    OperatingProgressDisplay Collection)
{
    public static ProjectOperatingDisplay Create(
        ProjectSummaryDto project,
        FinanceProjectSummaryDto finance)
    {
        var cashGap = Math.Max(finance.UnpaidAmount - finance.UncollectedAmount, 0m);
        return new ProjectOperatingDisplay(
            project.CurrentAmount,
            project.SettledAmount,
            finance.OutputInvoiceAmount,
            finance.CollectedAmount,
            finance.PaidAmount,
            finance.UncollectedAmount,
            finance.UnpaidAmount,
            cashGap,
            finance.UncollectedAmount - finance.UnpaidAmount,
            Progress("settlement", "结算进度", project.CurrentAmount, project.SettledAmount),
            Progress("invoice", "开票进度", finance.ReceivableAmount, finance.OutputInvoiceAmount),
            Progress("collection", "回款进度", finance.ReceivableAmount, finance.CollectedAmount));
    }

    private static OperatingProgressDisplay Progress(
        string key,
        string label,
        decimal total,
        decimal completed)
    {
        var remaining = Math.Max(total - completed, 0m);
        var percentage = total <= 0m
            ? 0m
            : decimal.Round(Math.Clamp(completed / total * 100m, 0m, 100m), 2);
        return new OperatingProgressDisplay(key, label, total, completed, remaining, percentage);
    }
}
```

该类型只能组合已有 DTO，不读取数据库、不缓存、不参与保存。

- [ ] **Step 4: 调整项目详情首屏。**

在 `Details.cshtml` 使用：

```csharp
var operating = ProjectOperatingDisplay.Create(item.ProjectSummary, item.FinanceSummary);
```

将首屏摘要固定为八项：当前项目金额、已结算、已开票、已收、已付、待收、待付、现金缺口。项目阶段继续使用现有 `ProjectStage` 标签，不新增施工进度。

把现有收款/开票/付款三条小进度改为结算/开票/回款三条，统一显示“已完成、总额、剩余、百分比”。现有工程量、收款、开票、付款和施工标签页保持原位置、字段和保存处理器不变。

- [ ] **Step 5: 增加局部响应式样式。**

桌面端经营摘要使用四列两行，1180px 以下两列，窄屏保持两列；三项进度桌面三列、760px 以下一列。不得修改全局主题色、导航尺寸或其他模块卡片。

- [ ] **Step 6: 更新页面断言并运行 GREEN。**

`ProjectAuthorizationTests` 断言页面包含“结算进度、开票进度、回款进度、现金缺口”，且不包含“施工完成率”。`ResponsiveUiAssetTests` 断言新网格具有 1180px 和 760px 降级规则。

重复 Step 2，并加上两个页面测试过滤器。Expected: PASS。

---

## Task 3: 在现有首页增加轻量项目现金关注列表

**Files:**

- Modify `src/EngineeringManager.Application/Dashboard/DashboardDtos.cs`
- Modify `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- Modify `src/EngineeringManager.Web/Pages/Index.cshtml`
- Modify `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify `tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`
- Modify `tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] **Step 1: 写首页 DTO 和排序失败测试。**

增加：

```csharp
public sealed record DashboardProjectCashDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    ProjectStage Stage,
    decimal CollectedAmount,
    decimal PaidAmount,
    decimal UncollectedAmount,
    decimal UnpaidAmount,
    decimal CashGap);
```

测试准备三个授权项目，断言 `ProjectCashItems` 按 `CashGap` 降序、再按 `UncollectedAmount` 降序，且不包含未授权项目。现金缺口使用 `Math.Max(UnpaidAmount - UncollectedAmount, 0m)`。

- [ ] **Step 2: 运行 RED。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~DashboardServiceTests|FullyQualifiedName~HomePageTests"
```

Expected: FAIL，因为首页 DTO 尚无现金合计和项目现金行。

- [ ] **Step 3: 最小扩展 DashboardDto。**

在现有构造函数末尾增加可选参数，保留现有测试和假服务兼容：

```csharp
decimal uncollectedAmount = 0m,
decimal unpaidAmount = 0m,
decimal cashGap = 0m,
IReadOnlyList<DashboardProjectCashDto>? projectCashItems = null
```

对应只读属性使用 `projectCashItems ?? []`。不得删除现有月度趋势、设备、工资、提醒和阶段分布字段。

- [ ] **Step 4: 使用 Task 1 的财务结果组装首页。**

`DashboardService` 从 `financeOverview.Projects` 与已授权项目字典组装现金行，排序后取前 10 条：

```csharp
var cashItems = financeOverview.Projects
    .Select(item =>
    {
        var project = projects.Single(project => project.Id == item.ProjectId);
        var cashGap = Math.Max(item.Summary.UnpaidAmount - item.Summary.UncollectedAmount, 0m);
        return new DashboardProjectCashDto(
            project.Id,
            project.ProjectNumber,
            project.Name,
            project.Stage,
            item.Summary.CollectedAmount,
            item.Summary.PaidAmount,
            item.Summary.UncollectedAmount,
            item.Summary.UnpaidAmount,
            cashGap);
    })
    .OrderByDescending(item => item.CashGap)
    .ThenByDescending(item => item.UncollectedAmount)
    .ThenBy(item => item.ProjectNumber)
    .Take(10)
    .ToArray();
```

首页三条进度改为：结算进度、开票进度、回款进度。结算使用所有授权项目的 `SettledAmount / CurrentAmount`；开票和回款使用 Task 1 的财务汇总。项目阶段分布继续使用现有定义。

- [ ] **Step 5: 调整首页信息顺序。**

首页首屏五项为：在管项目、当前项目金额、项目待收、项目待付、现金缺口。新增“项目现金关注”表，显示项目、阶段、已收、已付、待收、待付、现金缺口；项目名称链接到现有项目详情，标题区提供进入现有 `/Finance` 页的入口。

完整组合筛选、排序、个人视图和导出继续由现有财务页负责，首页不复制 `SavedDataViewService`，不增加第二套筛选器。现有月度趋势、设备、工资、提醒和离线状态保留在后续区域。

- [ ] **Step 6: 更新首页测试并运行 GREEN。**

`HomePageTests` 的假服务增加两条现金行，断言：

```csharp
html.Should().Contain("data-project-cash-watch");
html.Should().Contain("结算进度");
html.Should().Contain("开票进度");
html.Should().Contain("回款进度");
html.Should().Contain("现金缺口");
```

匿名首页仍不得包含 `data-business-dashboard` 或任何经营金额。重复 Step 2。Expected: PASS。

---

## Task 4: 将现有并发错误升级为通用弹出提示

**Files:**

- Modify `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create `src/EngineeringManager.Web/wwwroot/js/components/conflict-notice.js`
- Modify `src/EngineeringManager.Web/wwwroot/js/site.js`
- Modify `src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- Modify `tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`
- Modify `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] **Step 1: 写前端资源失败测试。**

断言布局存在 `data-conflict-notice`、`role="alertdialog"`、刷新和关闭按钮；`site.js` 加载 `conflict-notice.js`；组件识别“已被其他用户修改”“并发”“刷新后重试”三个关键提示，并使用 `window.location.reload()` 刷新。

- [ ] **Step 2: 写项目并发页面失败测试。**

使用过期 `ConcurrencyStamp` 提交项目快捷编辑，断言返回页面仍保留编辑区，并包含服务已有的“已被其他用户修改，请刷新后重试”消息。服务端现有并发规则不改。

- [ ] **Step 3: 运行 RED。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~ProjectAuthorizationTests"
```

Expected: FAIL，因为全局冲突提示资源尚不存在。

- [ ] **Step 4: 增加全局提示容器。**

在 `_Layout.cshtml` 的页面内容之后增加：

```html
<div class="conflict-notice" data-conflict-notice role="alertdialog" aria-modal="true" aria-labelledby="conflict-notice-title" hidden>
    <div class="conflict-notice__surface">
        <h2 id="conflict-notice-title">数据已经发生变化</h2>
        <p data-conflict-message>该记录已被其他用户修改，请刷新后查看最新数据。</p>
        <div class="button-row">
            <button class="button button--primary" type="button" data-conflict-refresh>刷新页面</button>
            <button class="button button--secondary" type="button" data-conflict-close>关闭</button>
        </div>
    </div>
</div>
```

容器默认隐藏，不改变任何正常页面布局。

- [ ] **Step 5: 实现冲突识别和交互。**

`conflict-notice.js` 导出：

```javascript
export function initConflictNotice() {
  const root = document.querySelector("[data-conflict-notice]");
  if (!root) return;

  const messages = [...document.querySelectorAll(".validation-summary-errors li")]
    .map((item) => item.textContent?.trim() ?? "")
    .filter(Boolean);
  const conflict = messages.find((message) =>
    /已被其他用户修改|并发|刷新后重试/.test(message));
  if (!conflict) return;

  root.querySelector("[data-conflict-message]").textContent = conflict;
  root.hidden = false;
  root.querySelector("[data-conflict-refresh]").addEventListener("click", () => window.location.reload());
  root.querySelector("[data-conflict-close]").addEventListener("click", () => { root.hidden = true; });
  root.querySelector("[data-conflict-refresh]").focus();
}
```

`site.js` 在 DOM 初始化时调用该函数。禁止自动覆盖、自动重试或自动合并旧表单数据。

- [ ] **Step 6: 增加克制的全局样式并运行 GREEN。**

提示层使用半透明遮罩、最大宽度 32rem、8px 以内圆角和现有按钮样式；390px 下左右保留至少 16px 间距。重复 Step 3。Expected: PASS。

---

## Task 5: 定向回归、性能和真实页面验收

**Files:**

- Modify `docs/开发进度.md` only after all implementation and verification pass.
- Modify `EngineeringManager/README.md` only if the dashboard behavior description is now inaccurate.

- [ ] **Step 1: 运行定向测试。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~FinanceSummaryTests|FullyQualifiedName~DashboardServiceTests|FullyQualifiedName~ProjectOperatingDisplayTests|FullyQualifiedName~HomePageTests|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~ResponsiveUiAssetTests"
```

Expected: PASS。

- [ ] **Step 2: 运行完整质量门禁。**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

Expected: `QUALITY_GATE=PASS`，Release 构建 0 警告、0 错误，全部测试通过。

- [ ] **Step 3: 验证性能基线。**

运行现有代表性性能测试，确认常用列表/详情不超过 2 秒、首页汇总不超过 3 秒。若 Task 1 的指定项目汇总造成 N+1 回归，只优化该查询实现，不引入缓存表、后台同步或新数据库结构。

- [ ] **Step 4: 启动服务并做真实桌面验收。**

验证：

- 首页只显示当前用户授权项目。
- 首页三条进度为结算、开票、回款。
- 项目现金关注表排序正确，项目链接可用。
- 项目详情八项摘要与明细合计一致。
- 项目阶段仍完全使用现有定义，没有施工进度百分比。
- 现有项目、财务、工程量和施工原位编辑不受影响。
- 使用过期版本提交时弹出冲突提示，关闭不覆盖，刷新读取最新值。

- [ ] **Step 5: 做 390px 最终响应式验收。**

检查首页、项目详情和冲突提示不存在横向溢出、文字遮挡和按钮溢出。按项目既有约定，这一步作为本轮功能完成后的移动端总验收，不在每个中间步骤重复执行。

- [ ] **Step 6: 检查差异并更新进度文档。**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

进度文档只记录实际完成和实际验证结果，不提前宣称通过。不得把测试数据库、日志、截图或临时浏览器文件加入工作区。

---

## 3. 最终验收矩阵

- [ ] 首页与项目详情的已收、已付、待收、待付来自同一现有财务服务口径。
- [ ] 结算进度使用 `SettledAmount / CurrentAmount`。
- [ ] 开票进度使用 `OutputInvoiceAmount / ReceivableAmount`。
- [ ] 回款进度使用 `CollectedAmount / ReceivableAmount`。
- [ ] 现金缺口使用 `max(UnpaidAmount - UncollectedAmount, 0)`。
- [ ] 项目只显示现有项目阶段，不新增施工进度管理。
- [ ] 首页现金关注列表只包含授权项目。
- [ ] 完整筛选、个人视图和导出继续复用现有财务页。
- [ ] 并发冲突弹窗不允许静默覆盖。
- [ ] 员工、工资、临时人员合并任务没有被本计划重复修改。
- [ ] 中央财务账本、分摊、年度账和阶段对账点没有被本计划提前实现。
- [ ] 不包含任何批量处理功能。
- [ ] 不新增数据库迁移或业务数据副本。

## 4. 执行停止点

本计划文件完成后停止。只有用户明确发出“开始执行项目经营展示增量优化”或同等清晰命令，才从 Task 0 开始；在此之前不得修改应用代码、数据库或 Git 历史。
