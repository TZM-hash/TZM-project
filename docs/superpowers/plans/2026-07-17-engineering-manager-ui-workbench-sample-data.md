# UI、数据工作台与完整测试数据 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 安全重建 `EngineeringManager_Test` 并生成完整演示数据，以 `Reference` 为基准交付全局主题/动效设置、数据工作台、丰富图表和统一响应式 UI。

**Architecture:** 保持 .NET 10 模块化单体和 Razor Pages 服务器端渲染。设置与个人视图使用应用接口、Infrastructure EF Core 实现和 Web 页面三层边界；前端沿用四层 CSS 并把 Shell、表格、保存视图、图表和设置预览拆成按需加载的原生 JavaScript 模块。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、Identity、EF Core SQL Server、原生 JavaScript、SVG/Canvas/HTML、CSS、xUnit、FluentAssertions、SQLite 测试数据库、PowerShell 7。

---

## 文件结构

### 新增

- `EngineeringManager/src/EngineeringManager.Application/Settings/*`：显示设置 DTO 和接口。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Settings/SystemSettingsService.cs`：设置持久化、缓存和审计。
- `EngineeringManager/src/EngineeringManager.Application/DataViews/*`：个人视图 DTO、定义和接口。
- `EngineeringManager/src/EngineeringManager.Infrastructure/DataViews/SavedDataViewService.cs`：个人视图持久化。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/SystemSetting.cs`：全局设置实体。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/SavedDataView.cs`：用户个人视图实体。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataCatalog.cs`：固定样例规模、编号和日期规则。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`：完整关联数据构建。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataAssertions.cs`：播种后关系与金额验证。
- `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml*`：系统显示设置页。
- `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml`：共享数据工作台工具栏。
- `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_ChartEmptyState.cshtml`：统一图表空状态。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/core/*`：Shell、主题和动效。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/*`：表格、保存视图、筛选和图表。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js`：设置实时预览。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/img/icons.svg`：本地 SVG 图标精灵。
- `EngineeringManager/scripts/reset-test-database.ps1`：仅允许重建 `_Test` 数据库的脚本。
- `EngineeringManager/tests/EngineeringManager.Tests/Application/{SystemSettingsServiceTests,SavedDataViewServiceTests}.cs`：设置和个人视图服务测试。
- `EngineeringManager/tests/EngineeringManager.Tests/Web/{SystemSettingsPageTests,UiEffectsAssetTests,DataWorkbenchAssetTests,DataWorkbenchPageTests,ModuleDataWorkbenchTests,ChartAssetTests,ResponsiveUiAssetTests}.cs`：设置、资产、列表、图表和响应式测试。

### 修改

- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`：新增设置和个人视图映射。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationUser.cs`：个人视图导航属性。
- `EngineeringManager/src/EngineeringManager.Web/Program.cs`：注册服务并保持 Development 安全护栏。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Development/DevelopmentSampleDataSeeder.cs`：编排完整样例数据和多角色账号。
- `EngineeringManager/src/EngineeringManager.Application/Dashboard/*`、`Infrastructure/Dashboard/DashboardService.cs`：扩展趋势与设备/工资摘要。
- `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`、`_LoginPartial.cshtml`：新应用外壳。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/{base,components,pages,themes}.css`：移植并精简参考设计系统。
- 首页、登录页及 Projects、Finance、Employees、Payroll、EmployeeLedger、Partners、StageResults、Companies、Equipment、Reminders、DataExchange、Backups、Admin 页面。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`：缓存版本和新增静态资源。
- `docs/开发进度.md`：记录迭代进度、验证证据和后续计划。

## Task 1：建立完整样例数据的失败测试和安全目录

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataCatalog.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataAssertions.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`

- [ ] **Step 1: 写入样例规模和安全护栏失败测试**

```csharp
[Fact]
public void CatalogUsesConfirmedMediumScenario()
{
    SampleDataCatalog.CompanyCount.Should().Be(5);
    SampleDataCatalog.ProjectCount.Should().Be(15);
    SampleDataCatalog.EmployeeCount.Should().Be(30);
    SampleDataCatalog.PartnerCount.Should().Be(12);
    SampleDataCatalog.EquipmentCount.Should().Be(15);
}

[Theory]
[InlineData("Development", "EngineeringManager_Test", true)]
[InlineData("Development", "EngineeringManager", false)]
[InlineData("Production", "EngineeringManager_Test", false)]
public void ResetGuardMatchesOnlyDevelopmentTestDatabase(string environment, string database, bool allowed)
{
    var action = () => DevelopmentSampleDataSeeder.ValidateSafety(environment, database);
    if (allowed) action.Should().NotThrow(); else action.Should().Throw<InvalidOperationException>();
}
```

- [ ] **Step 2: 运行定向测试并确认因缺少 Catalog 失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter DevelopmentSampleDataSeederTests
```

预期：编译失败，提示 `SampleDataCatalog` 不存在。

- [ ] **Step 3: 实现固定规模和一致性断言入口**

```csharp
public static class SampleDataCatalog
{
    public const int CompanyCount = 5;
    public const int ProjectCount = 15;
    public const int EmployeeCount = 30;
    public const int PartnerCount = 12;
    public const int EquipmentCount = 15;
    public static DateOnly AnchorDate(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}

public static class SampleDataAssertions
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"样例数据一致性失败：{message}");
    }
}
```

- [ ] **Step 4: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter DevelopmentSampleDataSeederTests
git add src/EngineeringManager.Infrastructure/Development tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs
git commit -m 'test: define complete development sample scenario'
```

## Task 2：生成组织、账号、项目和合同样例

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/DevelopmentSampleDataSeeder.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`

- [ ] **Step 1: 写入 SQLite 集成测试，要求五家公司、十五个项目和多角色账号**

```csharp
[Fact]
public async Task SeederCreatesConfirmedCoreScenarioWithoutDuplicates()
{
    await using var fixture = await SampleSeederFixture.CreateAsync();
    await fixture.SeedAsync();
    await fixture.SeedAsync();

    (await fixture.Db.LegalEntities.CountAsync()).Should().Be(5);
    (await fixture.Db.Projects.CountAsync()).Should().Be(15);
    (await fixture.Db.Contracts.CountAsync()).Should().BeInRange(18, 20);
    (await fixture.Db.Employees.CountAsync()).Should().Be(30);
    foreach (var role in new[] { SystemRoles.SystemAdministrator, SystemRoles.ApplicationAdministrator, SystemRoles.Finance, SystemRoles.ProjectManager, SystemRoles.SiteStaff })
        (await fixture.UserManager.GetUsersInRoleAsync(role)).Should().NotBeEmpty();
}
```

- [ ] **Step 2: 运行测试并确认数量断言失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter SeederCreatesConfirmedCoreScenarioWithoutDuplicates
```

- [ ] **Step 3: 将播种器拆为幂等构建器并生成核心数据**

```csharp
public sealed class SampleDataBuilder(ApplicationDbContext db, UserManager<ApplicationUser> users, TimeProvider timeProvider)
{
    public async Task<SampleDataContext> BuildCoreAsync(CancellationToken token)
    {
        var anchor = SampleDataCatalog.AnchorDate(timeProvider);
        var companies = await EnsureCompaniesAsync(token);
        var accounts = await EnsureUsersAsync(token);
        var partners = await EnsurePartnersAsync(token);
        var projects = await EnsureProjectsAndContractsAsync(anchor, companies, partners, accounts, token);
        return new SampleDataContext(anchor, companies, partners, projects, accounts);
    }
}
```

实现编号前缀 `DEMO-COMP-*`、`DEMO-P-*`、`DEMO-C-*`、`DEMO-E-*` 和 `demo-*` 用户名；多数项目绑定一家签约公司，项目 04、09 分别拆为两家和三家公司合同记录。

- [ ] **Step 4: 将凭据统一写入 Git 忽略文件并验证幂等**

凭据文件按角色列出用户名、密码、显示名和数据库名；密码由现有 `GenerateTestPassword()` 生成并满足 Identity 规则，不在日志中输出。

- [ ] **Step 5: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter DevelopmentSampleDataSeederTests
git add src/EngineeringManager.Infrastructure/Development tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs
git commit -m 'feat: seed realistic organizations projects and test users'
```

## Task 3：补齐财务、工资、设备、阶段成果和提醒样例

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataAssertions.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/DevelopmentSampleDataSeeder.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`

- [ ] **Step 1: 写入完整业务链断言测试**

```csharp
[Fact]
public async Task SeederCreatesTwelveMonthBalancedBusinessScenario()
{
    await using var fixture = await SampleSeederFixture.CreateAsync();
    await fixture.SeedAsync();

    (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(12);
    (await fixture.Db.Equipment.CountAsync()).Should().Be(15);
    (await fixture.Db.ReceivableEntries.CountAsync()).Should().BeGreaterThan(20);
    (await fixture.Db.CollectionEntries.CountAsync()).Should().BeGreaterThan(15);
    (await fixture.Db.InvoiceEntries.CountAsync()).Should().BeGreaterThan(15);
    (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(12);
    (await fixture.Db.StageResults.CountAsync()).Should().BeGreaterThan(10);
    (await fixture.Db.ReminderItems.CountAsync()).Should().BeGreaterThan(5);
}
```

- [ ] **Step 2: 运行测试并确认业务表数量失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter SeederCreatesTwelveMonthBalancedBusinessScenario
```

- [ ] **Step 3: 生成近十二个月的财务和员工数据**

每月生成应收/收款/销项发票、应付/付款、工资批次和工资支付；使用确定性比例形成已收、未收、已开票、未开票、已付和未付场景。员工往来至少包含报销、借支、奖金、利息和分红各两笔。

- [ ] **Step 4: 生成设备和现场数据**

创建自有/租赁设备、所属公司、租赁协议、项目使用、进退场、施工/停工多段日期、维修记录、结算和调整项；创建阶段成果、工程量行及测试附件元数据，并把占位文件写入 `App_Data/attachments`。

- [ ] **Step 5: 在保存后执行金额和关系断言**

```csharp
SampleDataAssertions.Require(receivable >= collected, "已收款不得大于应收款");
SampleDataAssertions.Require(receivable >= invoiced, "已开票不得大于应开票");
SampleDataAssertions.Require(payable >= paid + deductions, "付款与扣款不得超过应付");
SampleDataAssertions.Require(usage.WorkPeriods.Sum(x => x.Days) <= usage.TotalCalendarDays, "施工停工天数不得超过进退场区间");
```

- [ ] **Step 6: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter DevelopmentSampleDataSeederTests
git add src/EngineeringManager.Infrastructure/Development tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs
git commit -m 'feat: seed complete finance payroll equipment and field data'
```

## Task 4：建立安全测试库重建脚本

**Files:**
- Create: `EngineeringManager/scripts/reset-test-database.ps1`
- Modify: `EngineeringManager/README.md`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`

- [ ] **Step 1: 写入脚本静态安全测试**

```csharp
[Fact]
public void ResetScriptRequiresDevelopmentAndTestSuffix()
{
    var script = RepositoryFile.Read("scripts", "reset-test-database.ps1");
    script.Should().Contain("$ErrorActionPreference = 'Stop'");
    script.Should().Contain("-notmatch '_Test$'");
    script.Should().Contain("ASPNETCORE_ENVIRONMENT = 'Development'");
    script.Should().NotContain("EngineeringManager_Production");
}
```

- [ ] **Step 2: 实现 PowerShell 7 脚本**

```powershell
$ErrorActionPreference = 'Stop'
param([string]$DatabaseName = 'EngineeringManager_Test')
if ($DatabaseName -notmatch '_Test$') { throw '只允许重建名称以 _Test 结尾的测试数据库。' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DevelopmentSampleData__Enabled = 'true'
& "$PSScriptRoot\dotnet.ps1" ef database drop --force --project "$PSScriptRoot\..\src\EngineeringManager.Infrastructure" --startup-project "$PSScriptRoot\..\src\EngineeringManager.Web"
& "$PSScriptRoot\dotnet.ps1" ef database update --project "$PSScriptRoot\..\src\EngineeringManager.Infrastructure" --startup-project "$PSScriptRoot\..\src\EngineeringManager.Web"
```

脚本必须先从 Development 连接串解析并比较实际数据库名，再执行 drop/update；上面的 `$DatabaseName` 只是第二重显式确认。

- [ ] **Step 3: 运行静态测试并提交，不在本任务实际删除数据库**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter ResetScriptRequiresDevelopmentAndTestSuffix
git add scripts/reset-test-database.ps1 README.md tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs
git commit -m 'build: add guarded test database reset workflow'
```

## Task 5：实现全局显示设置模型、服务、权限和审计

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/SystemSetting.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Settings/ISystemSettingsService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Settings/SystemSettingsService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`

- [ ] **Step 1: 写入默认值、保存和审计失败测试**

```csharp
[Fact]
public async Task DefaultsMatchConfirmedGlobalDisplayProfile()
{
    var settings = await fixture.Service.GetAsync(default);
    settings.Should().Be(new SystemDisplaySettings(VisualTheme.Default, MotionStyle.Technology, UiEffectsLevel.Medium, GlobalFont.SystemDefault, TableDensity.Standard));
}

[Fact]
public async Task SystemAdministratorSaveWritesBeforeAndAfterAudit()
{
    await fixture.Service.SaveAsync(new SettingsActor("sys", "系统管理员", true), fixture.HighGlassSettings, default);
    fixture.Db.AuditLogs.Should().ContainSingle(x => x.Action == "UpdateSystemDisplaySettings" && x.BeforeJson != null && x.AfterJson != null);
}
```

- [ ] **Step 2: 运行测试并确认类型不存在**

- [ ] **Step 3: 实现 DTO、键值实体和服务**

```csharp
public sealed record SystemDisplaySettings(VisualTheme Theme, MotionStyle Motion, UiEffectsLevel Effects, GlobalFont Font, TableDensity Density);
public sealed record SettingsActor(string UserId, string UserName, bool CanManage);

public interface ISystemSettingsService
{
    Task<SystemDisplaySettings> GetAsync(CancellationToken token);
    Task SaveAsync(SettingsActor actor, SystemDisplaySettings settings, CancellationToken token);
}
```

服务使用 `IMemoryCache` 缓存一个组合设置对象；保存前验证枚举，应用管理员 `CanManage=false` 时抛出 `UnauthorizedAccessException`，成功保存后写 `AuditLog` 并移除缓存。

- [ ] **Step 4: 配置实体和 DI，运行测试**

`SystemSetting.Key` 最大 100、唯一索引；`Value` 最大 500；`UpdatedByUserId` 可空并使用 `SetNull` 删除行为。

- [ ] **Step 5: 生成 Migration 并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 ef migrations add AddSystemDisplaySettings --project .\src\EngineeringManager.Infrastructure --startup-project .\src\EngineeringManager.Web --output-dir Data\Migrations
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter SystemSettingsServiceTests
git add src/EngineeringManager.Application/Settings src/EngineeringManager.Infrastructure/Settings src/EngineeringManager.Infrastructure/Data src/EngineeringManager.Web/Program.cs tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs
git commit -m 'feat: add audited global display settings'
```

## Task 6：建立设置页和 Reference 风格应用外壳

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_LoginPartial.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/img/icons.svg`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs`

- [ ] **Step 1: 写入系统管理员可写、应用管理员只读测试**

```csharp
[Theory]
[InlineData(SystemRoles.SystemAdministrator, HttpStatusCode.OK, false)]
[InlineData(SystemRoles.ApplicationAdministrator, HttpStatusCode.OK, true)]
public async Task SettingsPageHonorsAdministratorLevel(string role, HttpStatusCode status, bool readOnly)
{
    var html = await factory.ForRole(role).CreateClient().GetStringAsync("/Admin/Settings");
    html.Should().Contain("显示与交互设置");
    html.Contains("data-settings-readonly").Should().Be(readOnly);
}
```

- [ ] **Step 2: 实现设置页卡片和实时预览标记**

页面包含 `data-theme-option`、`data-motion-option`、`data-effects-option`、`data-global-font-select`、`data-table-density`，应用管理员禁用表单控件且不渲染保存按钮。

- [ ] **Step 3: 重构 Layout**

```cshtml
@inject ISystemSettingsService SettingsService
@{
    var display = await SettingsService.GetAsync(Context.RequestAborted);
}
<body class="app-shell @display.ThemeCssClass @display.MotionCssClass @display.EffectsCssClass @display.FontCssClass @display.DensityCssClass">
```

加入背景效果层、可收缩侧栏、分组导航、顶部栏、提醒/网络状态和用户菜单；使用路由判断 active，不硬编码首页 active。

- [ ] **Step 4: 运行 Web 测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'SystemSettingsPageTests|HomePageTests|AdminAuthorizationTests'
git add src/EngineeringManager.Web/Pages src/EngineeringManager.Web/wwwroot/img tests/EngineeringManager.Tests/Web
git commit -m 'feat: add reference-style shell and display settings page'
```

## Task 7：移植四层视觉系统、主题和动效模块

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/base.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/core/shell.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/core/effects.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

- [ ] **Step 1: 写入资产结构和减少动态效果测试**

```csharp
[Fact]
public void AssetsContainConfirmedThemesEffectsAndReducedMotion()
{
    var css = FrontendAssetReader.ReadCss();
    var js = FrontendAssetReader.ReadJavaScript();
    css.Should().Contain("body.theme-clear-glass");
    css.Should().Contain("body.motion-apple.ui-effects-high");
    css.Should().Contain("@media (prefers-reduced-motion: reduce)");
    js.Should().Contain("initThemePreview").And.Contain("initSidebar");
}
```

- [ ] **Step 2: 移植 `Reference` 令牌与组件，统一为本项目类名**

保留 `--app-bg`、`--app-surface`、`--app-primary`、状态色、圆角、阴影、侧栏宽度和字体栈；移除繁简语言选择器、甘特专属样式和无关业务组件。

- [ ] **Step 3: 实现按需加载和三档动效**

```javascript
import { initShell } from "./core/shell.js";
import { initEffects } from "./core/effects.js";

const jobs = [initShell(), initEffects()];
if (document.querySelector("[data-theme-option], [data-motion-option]"))
  jobs.push(import("./pages/settings.js").then((m) => m.initSettingsPreview()));
await Promise.all(jobs);
```

- [ ] **Step 4: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter UiEffectsAssetTests
git add src/EngineeringManager.Web/wwwroot tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs
git commit -m 'feat: port reference visual themes and effects'
```

## Task 8：实现个人保存视图持久化

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/SavedDataView.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationUser.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/DataViews/DataViewDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/DataViews/ISavedDataViewService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/DataViews/SavedDataViewService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/SavedDataViewServiceTests.cs`

- [ ] **Step 1: 写入用户隔离、默认唯一和失效列兼容测试**

```csharp
[Fact]
public async Task DefaultViewIsUniquePerUserAndPage()
{
    await service.SaveAsync("u1", new SaveDataViewRequest("projects", "施工中", true, filterJson, columnJson, "ContractAmount", false, TableDensity.Compact, 50), default);
    await service.SaveAsync("u1", new SaveDataViewRequest("projects", "未收款", true, secondFilter, columnJson, "Uncollected", true, TableDensity.Standard, 20), default);
    (await service.ListAsync("u1", "projects", definition, default)).Should().ContainSingle(x => x.IsDefault);
}
```

- [ ] **Step 2: 实现服务并限制页面键、名称、JSON 大小和 PageSize**

`PageKey` 最大 100、名称最大 100、FilterJson/ColumnJson 最大 8000；PageSize 只接受 20/50/100。读取时使用页面定义过滤已删除字段。

- [ ] **Step 3: 生成 Migration、运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 ef migrations add AddSavedDataViews --project .\src\EngineeringManager.Infrastructure --startup-project .\src\EngineeringManager.Web --output-dir Data\Migrations
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter SavedDataViewServiceTests
git add src/EngineeringManager.Application/DataViews src/EngineeringManager.Infrastructure/DataViews src/EngineeringManager.Infrastructure/Data src/EngineeringManager.Web/Program.cs tests/EngineeringManager.Tests/Application/SavedDataViewServiceTests.cs
git commit -m 'feat: add user-scoped saved data views'
```

## Task 9：实现共享数据工作台组件

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchViewModel.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/data-table.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/saved-views.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/filter-drawer.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs`

- [ ] **Step 1: 写入列管理、行距、筛选和保存视图资产测试**

```csharp
[Fact]
public void WorkbenchSupportsConfirmedInteractionSet()
{
    var js = FrontendAssetReader.ReadJavaScript();
    js.Should().Contain("data-column-key");
    js.Should().Contain("data-column-order");
    js.Should().Contain("row-spacing-compact");
    js.Should().Contain("data-filter-chip");
    js.Should().Contain("data-saved-view-filter-json");
    js.Should().Contain("data-current-page-size");
}
```

- [ ] **Step 2: 实现列显示、拖动顺序、固定列和行距**

临时状态使用页面+表格 ID 作为 `localStorage` 键；固定列不允许隐藏；至少保留一列；服务端保存视图优先于浏览器临时状态。

- [ ] **Step 3: 实现高级筛选、标签和保存视图序列化**

筛选表单写入 URL；空值从 URL 移除；标签单独移除对应参数；保存时序列化允许字段白名单，不保存防伪令牌或用户输入以外的隐藏字段。

- [ ] **Step 4: 运行静态测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter DataWorkbenchAssetTests
git add src/EngineeringManager.Web/Pages/Shared src/EngineeringManager.Web/wwwroot/js tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs
git commit -m 'feat: add reusable data workbench interactions'
```

## Task 10：项目和财务页面接入服务端筛选、排序、分页和导出联动

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/IProjectService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/FinanceDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Finance/Index.cshtml*`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`

- [ ] **Step 1: 写入查询 DTO 和服务端行为测试**

```csharp
var query = new ProjectListQuery("市政", [ProjectStage.UnderConstruction], companyId, managerId, 100_000m, 5_000_000m, "CurrentAmount", true, 2, 20);
var result = await service.SearchProjectsAsync(actor, query, default);
result.Items.Should().OnlyContain(x => x.Name.Contains("市政") && x.CurrentAmount >= 100_000m);
result.Page.Should().Be(2);
```

- [ ] **Step 2: 实现白名单排序、分页和权限范围**

所有 IQueryable 在排序和分页前应用角色/项目范围；不接受任意属性名反射排序；未知排序键回退到项目编号或业务日期。

- [ ] **Step 3: 接入共享工作台和当前筛选导出**

项目与财务页面分别定义允许列、系统预设视图和导出字段；导出请求复用同一查询 DTO，并从当前列状态提交字段键。

- [ ] **Step 4: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'ProjectServiceTests|FinanceLedgerServiceTests|DataWorkbenchPageTests'
git add src/EngineeringManager.Application/Projects src/EngineeringManager.Application/Finance src/EngineeringManager.Infrastructure/Projects src/EngineeringManager.Infrastructure/Finance src/EngineeringManager.Web/Pages/Projects src/EngineeringManager.Web/Pages/Finance tests/EngineeringManager.Tests
git commit -m 'feat: add project and finance data workspaces'
```

## Task 11：其余业务列表接入数据工作台

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/EmployeeLedger/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/StageResults/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Reminders/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Backups/Index.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/{Users,Organizations}.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Employees/{EmployeeDtos,IEmployeeService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Employees/EmployeeService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Payroll/{PayrollDtos,IPayrollService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/EmployeeLedger/{EmployeeLedgerDtos,IEmployeeLedgerService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/EmployeeLedger/EmployeeLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Partners/{PartnerDtos,IBusinessPartnerService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/StageResults/{StageResultDtos,IStageResultService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/StageResults/StageResultService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Companies/{CompanyDtos,ICompanyManagementService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Equipment/{EquipmentDtos,IEquipmentService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Equipment/EquipmentService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Reminders/{ReminderDtos,IReminderService}.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Reminders/ReminderService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ModuleDataWorkbenchTests.cs`

- [ ] **Step 1: 写入主要页面存在工作台标记和权限不回归测试**

```csharp
[Theory]
[InlineData("/Employees")]
[InlineData("/Payroll")]
[InlineData("/EmployeeLedger")]
[InlineData("/Partners")]
[InlineData("/StageResults")]
[InlineData("/Companies")]
[InlineData("/Equipment")]
[InlineData("/Reminders")]
public async Task MajorListUsesSharedWorkbench(string url)
{
    var html = await authorizedClient.GetStringAsync(url);
    html.Should().Contain("data-workbench").And.Contain("data-column-manager-table");
}
```

- [ ] **Step 2: 为各模块定义稳定页面键、可见列和预设视图**

使用 `employees`、`payroll`、`employee-ledger`、`partners`、`stage-results`、`companies`、`equipment`、`reminders` 等固定 PageKey；预设视图与设计文档一致。

- [ ] **Step 3: 接入列管理、行距、筛选标签、分页和当前筛选导出**

写操作按钮继续按现有权限显示；查询用户可使用筛选和导出，但不获得新增、编辑或删除能力。

- [ ] **Step 4: 运行模块授权和工作台测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'ModuleDataWorkbenchTests|AuthorizationTests|CompanyPageTests|EquipmentPageTests'
git add src/EngineeringManager.Application src/EngineeringManager.Infrastructure src/EngineeringManager.Web/Pages tests/EngineeringManager.Tests
git commit -m 'feat: roll out data workbench across business modules'
```

## Task 12：扩展首页经营驾驶舱数据

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Dashboard/DashboardDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Index.cshtml`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] **Step 1: 写入十二个月趋势、设备和工资摘要测试**

```csharp
result.MonthlyTrend.Should().HaveCount(12);
result.MonthlyTrend.Should().Contain(x => x.Collected > 0m && x.Paid > 0m);
result.EquipmentSummary.Total.Should().BeGreaterThan(0);
result.EquipmentSummary.RentedCost.Should().BeGreaterThan(0m);
result.PayrollSummary.Unpaid.Should().BeGreaterThanOrEqualTo(0m);
```

- [ ] **Step 2: 使用数据库聚合投影扩展 Dashboard DTO**

新增 `DashboardMonthlyPointDto`、`DashboardEquipmentDto`、`DashboardPayrollDto`；避免加载完整实体图，月份不足补零；非财务用户不查询或返回金额趋势。

- [ ] **Step 3: 重做首页卡片和图表容器**

首页按风险、KPI、金额对比、月度趋势、项目阶段、设备/工资摘要和离线状态排列；所有图表提供 `data-chart-series` JSON 和可访问文字摘要。

- [ ] **Step 4: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'DashboardServiceTests|HomePageTests'
git add src/EngineeringManager.Application/Dashboard src/EngineeringManager.Infrastructure/Dashboard src/EngineeringManager.Web/Pages/Index.cshtml tests/EngineeringManager.Tests
git commit -m 'feat: build rich operating dashboard analytics'
```

## Task 13：实现本地图表渲染和模块图形摘要

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/charts.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_ChartEmptyState.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/{Projects,Finance,Employees,Payroll,Companies,Equipment}/**/*.cshtml*`
- Modify: `EngineeringManager/src/EngineeringManager.Application/{Projects,Finance,Employees,Payroll,Companies,Equipment}/*.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/{Projects,Finance,Employees,Payroll,Companies,Equipment}/*.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ChartAssetTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/{ProjectServiceTests,FinanceSummaryTests,EmployeeServiceTests,PayrollServiceTests,CompanyManagementServiceTests,EquipmentServiceTests}.cs`

- [ ] **Step 1: 写入 SVG/Canvas、本地依赖和空状态测试**

```csharp
js.Should().Contain("renderLineChart").And.Contain("renderGroupedBars").And.Contain("renderProgressRing");
html.Should().Contain("data-chart-empty");
layout.Should().NotContain("cdn").And.NotContain("echarts").And.NotContain("chart.js");
```

- [ ] **Step 2: 实现可访问图表模块**

折线图使用 Canvas 并同步生成隐藏数据表；柱形图和进度图使用 SVG；尺寸变化通过 `ResizeObserver` 重绘；无数据时不创建画布；减少动态效果时直接渲染终态。

- [ ] **Step 3: 添加模块图形摘要**

项目、财务、员工/工资、自有公司和设备页面各使用设计文档中已确认的指标；图表点击只更新 GET 筛选参数或进入明细。

- [ ] **Step 4: 运行测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'ChartAssetTests|DashboardServiceTests|FinanceSummaryTests|CompanyManagementServiceTests|EquipmentServiceTests'
git add src/EngineeringManager.Application src/EngineeringManager.Infrastructure src/EngineeringManager.Web tests/EngineeringManager.Tests
git commit -m 'feat: add local accessible charts across modules'
```

## Task 14：统一登录、详情、表单、手机端和离线外壳

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Areas/Identity/Pages/Account/Login.cshtml`
- Modify: all primary detail/create/edit pages under `EngineeringManager/src/EngineeringManager.Web/Pages`
- Modify: four CSS layers and `wwwroot/service-worker.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/OfflineAssetsTests.cs`

- [ ] **Step 1: 写入响应式、触控和 PWA 缓存测试**

```csharp
css.Should().Contain("@media (max-width: 760px)");
css.Should().Contain("min-height: 44px");
css.Should().Contain("overflow-x: auto");
serviceWorker.Should().Contain("engineering-manager-shell-v3");
serviceWorker.Should().Contain("/js/components/data-table.js");
```

- [ ] **Step 2: 统一登录和业务页面结构**

移除页面内联样式，统一使用 page-header、form-section、detail-grid、status-badge、data-work-surface、empty-state 和 sticky-actions；保持所有现有字段、验证和 handler 名称。

- [ ] **Step 3: 完成手机端顺序和抽屉导航**

手机首页顺序为风险→KPI→金额→阶段→其他摘要；列表保留关键列并允许列管理；表单单列；复杂表格横向滚动；离线阶段成果和设备页面保持可操作。

- [ ] **Step 4: 运行 Web/PWA 测试并提交**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'ResponsiveUiAssetTests|OfflineAssetsTests|EquipmentOfflineAssetsTests|HomePageTests'
git add src/EngineeringManager.Web tests/EngineeringManager.Tests/Web
git commit -m 'feat: complete responsive reference-style interface'
```

## Task 15：应用 Migration、重建测试库并完成总验收

**Files:**
- Modify: `docs/开发进度.md`
- Generated artifacts remain under ignored `EngineeringManager/artifacts` and `App_Data` paths.

- [ ] **Step 1: 停止当前本地 Web 进程**

确认只终止本项目监听 `5075` 的 Development 进程，不终止 SQL Server 或其他应用。

- [ ] **Step 2: 运行安全脚本删除并重建 `EngineeringManager_Test`**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\reset-test-database.ps1 -DatabaseName 'EngineeringManager_Test'
```

预期：Migration 全部应用，完整样例数据生成，凭据文件更新；任何安全护栏失败立即停止。

- [ ] **Step 3: 运行完整测试和 Release 质量门禁**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

预期：全部测试通过，Release 0 警告、0 错误。

- [ ] **Step 4: 启动系统并进行浏览器验收**

使用本机测试管理员登录，检查桌面和手机视口下的首页、项目、财务、员工、公司、设备、系统设置、列管理、行距、高级筛选、个人视图、导出、主题和动效；检查控制台无错误，健康端点返回 200。

- [ ] **Step 5: 更新唯一开发进度文档**

记录每个任务提交、Migration、样例规模、测试结果、浏览器验收、当前分支、未提交用户文件和后续计划；不写测试密码。

- [ ] **Step 6: 提交最终集成状态并保持系统运行**

```powershell
$ErrorActionPreference = 'Stop'
git add docs/开发进度.md
git commit -m 'docs: record UI workbench and sample data completion'
git status --short --branch
```

仅提交本迭代实际修改；保留用户原有未提交改动。最终不推送远端，系统继续运行在 `http://localhost:5075`。
