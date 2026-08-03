# 圆润柔光外观模式实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在保留现有经典界面的前提下，新增可与全部颜色主题组合的“圆润柔光”全局外观模式，并覆盖卡片、弹窗、控件、按钮、菜单、表格容器和导航。

**Architecture:** 沿用现有 `SystemDisplaySettings` 键值保存机制，在显示设置中增加独立的 `UiAppearanceStyle` 维度，不新增数据库表或迁移。服务端在 `body` 输出外观类，`themes.css` 通过语义令牌和 `appearance-rounded-soft` 选择器统一覆盖共享组件，设置页 JavaScript 只负责保存前即时预览。

**Tech Stack:** ASP.NET Core 10 Razor Pages、C# records/enums、EF Core 键值设置表、原生 CSS 自定义属性、原生 ES modules、xUnit、FluentAssertions、PowerShell 7。

---

## 文件结构与职责

- `src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs`：定义外观枚举、默认值和 CSS 类映射。
- `src/EngineeringManager.Infrastructure/Settings/SystemSettingsService.cs`：读取、验证、保存 `Display.Appearance`，继续复用现有 `SystemSettings` 表和缓存。
- `src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml.cs`：设置页输入模型与应用设置互转。
- `src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml`：经典紧凑与圆润柔光两张预览卡。
- `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`：首屏输出正确的外观类，避免页面闪回。
- `src/EngineeringManager.Web/wwwroot/js/pages/settings.js`：设置页即时预览外观模式。
- `src/EngineeringManager.Web/wwwroot/js/site.js`：检测外观设置控件并按需加载设置预览模块。
- `src/EngineeringManager.Web/wwwroot/css/themes.css`：独立外观令牌、共享组件覆盖、主题组合和移动端例外。
- `tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`：默认值、映射、持久化、非法值和审计测试。
- `tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs`：设置页权限、字段和预览卡测试。
- `tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`：布局类、CSS 覆盖和 JavaScript 预览契约测试。
- `docs/项目部署手册.md`：补充外观模式的管理位置与升级兼容说明。

## 实施约束

- 当前工作区包含数据往返工作台等未提交修改；实施时不得重置、覆盖或批量格式化无关文件。
- `SystemSettings` 为现有键值表，本功能只新增 `Display.Appearance` 键，不生成 EF 迁移。
- 经典模式必须继续使用当前圆角和阴影，不通过全局改写基础值改变旧视觉。
- Git 暂存、提交和推送必须取得用户明确授权；下列提交步骤只作为授权后的执行清单。

---

### 任务 1：增加独立外观设置契约

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`

- [ ] **Step 1: 写失败测试，固定外观默认值和 CSS 类映射**

在 `SystemSettingsServiceTests` 增加：

```csharp
[Theory]
[InlineData(UiAppearanceStyle.Classic, "appearance-classic")]
[InlineData(UiAppearanceStyle.RoundedSoft, "appearance-rounded-soft")]
public void EveryAppearanceStyleMapsToItsExpectedCssClass(
    UiAppearanceStyle appearance,
    string expectedCssClass)
{
    var settings = SystemDisplaySettings.Default with { Appearance = appearance };

    settings.AppearanceCssClass.Should().Be(expectedCssClass);
}

[Fact]
public void DefaultAppearanceStyleKeepsExistingClassicVisuals()
{
    SystemDisplaySettings.Default.Appearance.Should().Be(UiAppearanceStyle.Classic);
    SystemDisplaySettings.Default.AppearanceCssClass.Should().Be("appearance-classic");
}
```

- [ ] **Step 2: 运行契约测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsServiceTests'
```

Expected: 编译失败，提示 `UiAppearanceStyle`、`Appearance` 或 `AppearanceCssClass` 尚不存在。

- [ ] **Step 3: 添加最小应用层契约，并保持现有构造调用兼容**

在 `SystemSettingsDtos.cs` 增加枚举，并把带默认值的新参数放在记录末尾：

```csharp
public enum UiAppearanceStyle
{
    Classic = 1,
    RoundedSoft = 2
}

public sealed record SystemDisplaySettings(
    VisualTheme Theme,
    MotionStyle Motion,
    UiEffectsLevel Effects,
    GlobalFont Font,
    TableDensity Density,
    GlobalFontSize FontSize,
    UiAppearanceStyle Appearance = UiAppearanceStyle.Classic)
{
    public static SystemDisplaySettings Default { get; } = new(
        VisualTheme.Default,
        MotionStyle.Technology,
        UiEffectsLevel.Medium,
        GlobalFont.SystemDefault,
        TableDensity.Standard,
        GlobalFontSize.Standard,
        UiAppearanceStyle.Classic);

    public string AppearanceCssClass => Appearance switch
    {
        UiAppearanceStyle.RoundedSoft => "appearance-rounded-soft",
        _ => "appearance-classic"
    };
}
```

保留文件中已有的主题、动效、字体、密度和字号映射；不要用上述片段覆盖其他成员。

- [ ] **Step 4: 运行应用层定向测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsServiceTests'
```

Expected: 新增映射测试通过，现有主题和字号测试继续通过。

- [ ] **Step 5: 在获得 Git 明确授权后提交契约改动**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs' 'EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs'
git commit -m 'feat: add global appearance style contract'
```

---

### 任务 2：保存、读取并验证外观模式

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Settings/SystemSettingsService.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`

- [ ] **Step 1: 写失败测试，覆盖持久化、旧数据回退和非法值**

把默认设置断言扩展为包含 `UiAppearanceStyle.Classic`，并增加：

```csharp
[Fact]
public async Task RoundedSoftAppearancePersistsInExistingSettingsTable()
{
    await using var fixture = await Fixture.CreateAsync();
    var requested = SystemDisplaySettings.Default with
    {
        Appearance = UiAppearanceStyle.RoundedSoft
    };

    await fixture.Service.SaveAsync(
        new SettingsActor("sys", "系统管理员", true),
        requested,
        default);

    (await fixture.Service.GetAsync(default)).Appearance
        .Should().Be(UiAppearanceStyle.RoundedSoft);
    var stored = await fixture.Db.SystemSettings
        .SingleAsync(item => item.Key == "Display.Appearance");
    stored.Value.Should().Be("RoundedSoft");
}

[Fact]
public async Task MissingOrInvalidAppearanceSettingFallsBackToClassic()
{
    await using var fixture = await Fixture.CreateAsync();
    fixture.Db.SystemSettings.Add(new SystemSetting
    {
        Key = "Display.Appearance",
        Value = "UnknownAppearance",
        UpdatedByUserId = "sys"
    });
    await fixture.Db.SaveChangesAsync();

    (await fixture.Service.GetAsync(default)).Appearance
        .Should().Be(UiAppearanceStyle.Classic);
}

[Fact]
public async Task InvalidAppearanceIsRejectedBeforeAnySettingIsWritten()
{
    await using var fixture = await Fixture.CreateAsync();
    var invalid = SystemDisplaySettings.Default with
    {
        Appearance = (UiAppearanceStyle)999
    };

    var action = () => fixture.Service.SaveAsync(
        new SettingsActor("sys", "系统管理员", true),
        invalid,
        default);

    await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    (await fixture.Db.SystemSettings.CountAsync()).Should().Be(0);
}
```

同步将完整保存测试中的设置数量从 `6` 改为 `7`，并确认审计 JSON 包含 `RoundedSoft`。

- [ ] **Step 2: 运行测试并确认持久化测试失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsServiceTests'
```

Expected: 缺少 `Display.Appearance` 保存逻辑或保存数量仍为 6。

- [ ] **Step 3: 实现键值读写和校验**

在 `SystemSettingsService` 增加：

```csharp
private const string AppearanceKey = "Display.Appearance";
```

构造读取结果时追加：

```csharp
Parse(values, AppearanceKey, UiAppearanceStyle.Classic)
```

保存时追加：

```csharp
Upsert(existing, AppearanceKey, settings.Appearance.ToString(), actor.UserId);
```

校验条件调整为：

```csharp
if (!Enum.IsDefined(settings.Theme)
    || !Enum.IsDefined(settings.Motion)
    || !Enum.IsDefined(settings.Effects)
    || !Enum.IsDefined(settings.Font)
    || !Enum.IsDefined(settings.Density)
    || !Enum.IsDefined(settings.FontSize)
    || !Enum.IsDefined(settings.Appearance))
{
    throw new ArgumentOutOfRangeException(nameof(settings), "显示设置包含未知选项。");
}
```

审计原因更新为“维护全局主题、外观、动效、字体和表格密度”。不新增迁移。

- [ ] **Step 4: 运行设置服务测试并检查数据库键数量**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsServiceTests'
```

Expected: 全部通过，完整保存产生 7 个设置键，非法值不写入。

- [ ] **Step 5: 在获得 Git 明确授权后提交持久化改动**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/src/EngineeringManager.Infrastructure/Settings/SystemSettingsService.cs' 'EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs'
git commit -m 'feat: persist global appearance preference'
```

---

### 任务 3：在设置页增加外观选择和即时预览

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs`

- [ ] **Step 1: 写失败页面测试，固定字段、选项、预览卡和只读行为**

在系统管理员页面断言中增加：

```csharp
html.Should().Contain("name=\"Input.Appearance\"")
    .And.Contain("value=\"Classic\"")
    .And.Contain("value=\"RoundedSoft\"")
    .And.Contain("data-appearance-option=\"appearance-classic\"")
    .And.Contain("data-appearance-option=\"appearance-rounded-soft\"")
    .And.Contain("经典紧凑")
    .And.Contain("圆润柔光");
```

在只读测试继续断言没有提交按钮，并确认两个外观 radio 均输出 `disabled`。

- [ ] **Step 2: 运行页面测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsPageTests'
```

Expected: 页面尚无 `Input.Appearance` 和外观预览卡。

- [ ] **Step 3: 扩展设置页输入模型**

在 `InputModel` 增加：

```csharp
public UiAppearanceStyle Appearance { get; set; } = UiAppearanceStyle.Classic;
```

将互转实现调整为：

```csharp
public SystemDisplaySettings ToSettings() => new(
    Theme,
    Motion,
    Effects,
    Font,
    Density,
    FontSize,
    Appearance);

public static InputModel From(SystemDisplaySettings settings) => new()
{
    Theme = settings.Theme,
    Motion = settings.Motion,
    Effects = settings.Effects,
    Font = settings.Font,
    Density = settings.Density,
    FontSize = settings.FontSize,
    Appearance = settings.Appearance
};
```

- [ ] **Step 4: 增加两张外观预览卡**

在显示主题区块之后加入：

```html
<section class="form-section">
    <div class="form-section-heading">
        <div><p class="eyebrow">界面外观</p><h2>选择组件圆润程度</h2></div>
        <p>外观可与任意颜色主题组合，经典模式保持当前显示。</p>
    </div>
    <div class="option-card-grid" role="radiogroup" aria-label="界面外观">
        <label class="option-card" data-appearance-option="appearance-classic">
            <input asp-for="Input.Appearance" id="appearance-classic" type="radio" value="Classic" disabled="@Model.IsReadOnly" />
            <span class="option-preview option-preview--appearance-classic" aria-hidden="true"><i></i><i></i><i></i></span>
            <span><strong>经典紧凑</strong><small>保留当前卡片、按钮、表格和弹窗形态。</small></span>
        </label>
        <label class="option-card" data-appearance-option="appearance-rounded-soft">
            <input asp-for="Input.Appearance" id="appearance-rounded-soft" type="radio" value="RoundedSoft" disabled="@Model.IsReadOnly" />
            <span class="option-preview option-preview--appearance-rounded-soft" aria-hidden="true"><i></i><i></i><i></i></span>
            <span><strong>圆润柔光</strong><small>明显大圆角、柔和阴影和圆润控件。</small></span>
        </label>
    </div>
</section>
```

页面说明文字同步改为“主题、外观、动效、字体和表格密度”。

- [ ] **Step 5: 实现设置页即时预览**

在 `settings.js` 增加：

```javascript
const appearanceClasses = ["appearance-classic", "appearance-rounded-soft"];

function initAppearancePreview() {
  document.querySelectorAll("[data-appearance-option] input").forEach((input) => {
    input.addEventListener("change", () => {
      const selected = input.closest("[data-appearance-option]").dataset.appearanceOption;
      swapClass(appearanceClasses, selected);
    });
  });
}
```

并在 `initSettingsPreview()` 中调用：

```javascript
initAppearancePreview();
```

将 `site.js` 的设置模块检测条件调整为：

```javascript
if (document.querySelector("[data-theme-option], [data-appearance-option], [data-motion-option], [data-global-font-picker], [data-global-font-size-picker]")) {
  scheduleIdle(() => import("./pages/settings.js").then((module) => module.initSettingsPreview()));
}
```

- [ ] **Step 6: 运行设置页与静态资源测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~SystemSettingsPageTests|FullyQualifiedName~UiEffectsAssetTests'
```

Expected: 设置页字段、权限和即时预览资源测试通过。

- [ ] **Step 7: 在获得 Git 明确授权后提交设置页改动**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml.cs' 'EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml' 'EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js' 'EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js' 'EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs'
git commit -m 'feat: add rounded appearance selector'
```

---

### 任务 4：首屏应用外观类并建立全局圆润令牌

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

- [ ] **Step 1: 写失败资源测试，固定首屏类名和核心令牌**

在 `AssetsContainConfirmedThemesEffectsAndReducedMotion` 中增加：

```csharp
layout.Should().Contain("@displaySettings.AppearanceCssClass");
css.Should().Contain("body.appearance-classic")
    .And.Contain("body.appearance-rounded-soft")
    .And.Contain("--appearance-card-radius: 22px")
    .And.Contain("--appearance-dialog-radius: 26px")
    .And.Contain("--appearance-control-radius: 14px")
    .And.Contain("--appearance-card-shadow")
    .And.Contain("--appearance-overlay-shadow");
```

- [ ] **Step 2: 运行资源测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~UiEffectsAssetTests'
```

Expected: 布局和 CSS 尚未包含外观类与令牌。

- [ ] **Step 3: 首屏输出服务端外观类**

将 `_Layout.cshtml` 的 `body` 类扩展为：

```html
<body class="app-shell @displaySettings.ThemeCssClass @displaySettings.AppearanceCssClass @displaySettings.MotionCssClass @displaySettings.EffectsCssClass @displaySettings.FontCssClass @displaySettings.DensityCssClass">
```

不通过 `localStorage` 再覆盖服务端值，防止刷新时闪烁或与全局设置不一致。

- [ ] **Step 4: 在 `themes.css` 建立独立外观令牌**

在颜色主题规则之后、动效规则之前加入：

```css
body.appearance-classic {
  --appearance-card-radius: var(--app-radius-md);
  --appearance-dialog-radius: var(--app-radius-lg);
  --appearance-control-radius: var(--app-radius-sm);
  --appearance-menu-radius: var(--app-radius-md);
  --appearance-card-shadow: var(--app-shadow-soft);
  --appearance-overlay-shadow: var(--app-shadow);
  --appearance-highlight: transparent;
}

body.appearance-rounded-soft {
  --app-radius-sm: 12px;
  --app-radius-md: 18px;
  --app-radius-lg: 24px;
  --appearance-card-radius: 22px;
  --appearance-dialog-radius: 26px;
  --appearance-control-radius: 14px;
  --appearance-menu-radius: 18px;
  --appearance-card-shadow: 0 14px 38px rgba(var(--app-primary-rgb), .09), 0 3px 10px rgba(15, 23, 42, .045);
  --appearance-overlay-shadow: 0 30px 80px rgba(15, 23, 42, .18), 0 8px 24px rgba(var(--app-primary-rgb), .08);
  --appearance-highlight: inset 0 1px 0 rgba(255, 255, 255, .72);
}
```

这些令牌只在 `appearance-rounded-soft` 中改变全局圆角，经典模式继续使用现有基础令牌。

- [ ] **Step 5: 增加设置页外观预览图样式**

```css
.option-preview--appearance-classic {
  background: linear-gradient(145deg, #e9eef6, #f8fafc);
}
.option-preview--appearance-classic i { border-radius: 5px; }

.option-preview--appearance-rounded-soft {
  padding: .55rem;
  border-radius: 18px;
  background: linear-gradient(145deg, #eadfff, #fffaff 58%, #e2f7f0);
}
.option-preview--appearance-rounded-soft i {
  border-radius: 14px;
  box-shadow: 0 8px 18px rgba(var(--app-primary-rgb), .12), inset 0 1px 0 rgba(255,255,255,.8);
}
```

- [ ] **Step 6: 运行资源测试确认基础外观层通过**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~SystemSettingsPageTests'
```

Expected: 首屏类、外观令牌和预览卡测试通过。

- [ ] **Step 7: 在获得 Git 明确授权后提交外观基础层**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml' 'EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css' 'EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs'
git commit -m 'feat: add rounded soft appearance tokens'
```

---

### 任务 5：覆盖所有共享卡片、弹窗、控件、按钮和导航

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

- [ ] **Step 1: 写失败覆盖测试，固定核心组件类别**

在 `UiEffectsAssetTests` 增加独立测试：

```csharp
[Fact]
public void RoundedAppearanceCoversCardsDialogsControlsTablesMenusAndNavigation()
{
    var css = ReadCss();

    css.Should().Contain("body.appearance-rounded-soft :is(.panel")
        .And.Contain(".workbench-dialog")
        .And.Contain(".quick-edit-dialog")
        .And.Contain(".selection-dropdown-menu")
        .And.Contain(".column-manager-menu")
        .And.Contain(".project-workbook-export-popover")
        .And.Contain("body.appearance-rounded-soft :is(.button")
        .And.Contain("input:not([type=\"checkbox\"])")
        .And.Contain("body.appearance-rounded-soft :is(.table-wrap")
        .And.Contain("body.appearance-rounded-soft .nav-link")
        .And.Contain("body.appearance-rounded-soft .app-sidebar")
        .And.Contain("@media (max-width: 680px)")
        .And.Contain("body.appearance-rounded-soft .quick-edit-dialog");
}
```

- [ ] **Step 2: 运行资源测试并确认覆盖契约失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~UiEffectsAssetTests'
```

Expected: 圆润模式尚未覆盖全部共享组件类别。

- [ ] **Step 3: 覆盖大型卡片和页面容器**

在 `themes.css` 增加：

```css
body.appearance-rounded-soft :is(
  .panel,
  .metric-card,
  .form-section,
  .data-work-surface,
  .settings-select-card,
  .option-card,
  .exchange-card,
  .data-exchange-panel,
  .overview-strip article,
  .detail-grid > div,
  .inline-edit-panel,
  .quick-edit-body section,
  .auth-card
) {
  border-radius: var(--appearance-card-radius);
  box-shadow: var(--appearance-card-shadow), var(--appearance-highlight);
}
```

不要给表格中的每个 `td` 添加独立圆角或阴影。

- [ ] **Step 4: 覆盖弹窗、下拉和悬浮菜单**

```css
body.appearance-rounded-soft :is(
  .workbench-dialog,
  .quick-edit-dialog,
  .conflict-notice,
  .selection-dropdown-menu,
  .column-manager-menu,
  .project-workbook-export-popover,
  .toast-region
) {
  border-radius: var(--appearance-dialog-radius);
  box-shadow: var(--appearance-overlay-shadow), var(--appearance-highlight);
}

body.appearance-rounded-soft :is(
  .selection-dropdown-menu,
  .column-manager-menu,
  .project-workbook-export-popover
) {
  border-radius: var(--appearance-menu-radius);
}

body.appearance-rounded-soft .dialog-close {
  border-radius: 12px;
}
```

- [ ] **Step 5: 覆盖按钮和表单控件**

```css
body.appearance-rounded-soft :is(
  .button,
  .workspace-tab,
  .menu-toggle,
  .header-icon-button,
  .row-spacing-picker,
  .row-spacing-picker button,
  .dialog-close
) {
  border-radius: var(--appearance-control-radius);
}

body.appearance-rounded-soft :is(
  input:not([type="checkbox"]):not([type="radio"]),
  select,
  textarea,
  .selection-dropdown-toggle,
  .inline-cell-control
) {
  border-radius: var(--appearance-control-radius);
}

body.appearance-rounded-soft :is(
  input:not([type="checkbox"]):not([type="radio"]),
  select,
  textarea,
  .button
):focus-visible {
  box-shadow: 0 0 0 4px rgba(var(--app-primary-rgb), .12);
}
```

保留 `.pill`、`.filter-chip`、`.pwa-badge` 和进度条现有的 `999px` 胶囊圆角。

- [ ] **Step 6: 覆盖表格容器与导航，不破坏数据密度**

```css
body.appearance-rounded-soft :is(.table-wrap, .data-table-wrap) {
  overflow: clip;
  border-radius: var(--appearance-card-radius);
  box-shadow: var(--appearance-card-shadow), var(--appearance-highlight);
}

body.appearance-rounded-soft :is(.table-wrap, .data-table-wrap) > table {
  border-radius: inherit;
}

body.appearance-rounded-soft .data-workbench-toolbar {
  border-radius: 18px;
  box-shadow: 0 8px 24px rgba(var(--app-primary-rgb), .06), var(--appearance-highlight);
}

body.appearance-rounded-soft .nav-link,
body.appearance-rounded-soft .nav-submenu a {
  border-radius: 14px;
}

body.appearance-rounded-soft .nav-link.is-active,
body.appearance-rounded-soft .nav-submenu a.is-active {
  box-shadow: 0 12px 28px rgba(var(--app-primary-rgb), .18), var(--appearance-highlight);
}

body.appearance-rounded-soft .app-sidebar {
  border-radius: 0 24px 24px 0;
  box-shadow: 12px 0 42px rgba(15, 23, 42, .08), var(--appearance-highlight);
}

body.appearance-rounded-soft .app-header {
  box-shadow: 0 10px 32px rgba(15, 23, 42, .055);
}
```

如果 `overflow: clip` 会裁切现有列菜单，改为保留 `overflow-x: auto`，并使用表格外层已有容器控制圆角；不得隐藏菜单或滚动条。

- [ ] **Step 7: 增加窄屏例外**

```css
@media (max-width: 680px) {
  body.appearance-rounded-soft .app-sidebar {
    border-radius: 0 20px 20px 0;
  }

  body.appearance-rounded-soft .quick-edit-dialog {
    border-radius: 0;
  }

  body.appearance-rounded-soft .sticky-actions {
    border-radius: 18px 18px 0 0;
  }
}
```

移动端不得因圆角增加横向宽度，不得改变全屏快速编辑的高度和滚动策略。

- [ ] **Step 8: 运行资源、页面和现有列表 UI 测试**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~SystemSettingsPageTests|FullyQualifiedName~DataWorkbenchPageTests'
```

Expected: 圆润覆盖测试通过，现有数据工作台和弹窗契约不回归。

- [ ] **Step 9: 在获得 Git 明确授权后提交全局组件覆盖**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css' 'EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs'
git commit -m 'feat: round shared cards dialogs and controls'
```

---

### 任务 6：主题组合、人工验收和文档收口

**Files:**
- Modify: `EngineeringManager/docs/项目部署手册.md`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/base.css`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`

- [ ] **Step 1: 检查外观模式与三套颜色主题的组合边界**

确认以下六种组合都只由两个独立类组成：

```text
theme-default appearance-classic
theme-default appearance-rounded-soft
theme-clear-glass appearance-classic
theme-clear-glass appearance-rounded-soft
theme-lavender-cream appearance-classic
theme-lavender-cream appearance-rounded-soft
```

不得新增 `theme-lavender-rounded`、`theme-glass-rounded` 等组合类。

- [ ] **Step 2: 运行完整测试**

先停止占用 Web DLL 的本项目 5075 服务，再运行：

```powershell
$ErrorActionPreference = 'Stop'
dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --no-restore --configuration Release
```

Expected: 0 失败、0 跳过；测试总数以当次输出为准。

- [ ] **Step 3: 执行 Release 发布和差异检查**

```powershell
$ErrorActionPreference = 'Stop'
dotnet publish EngineeringManager/src/EngineeringManager.Web/EngineeringManager.Web.csproj --configuration Release --no-restore
git diff --check
```

Expected: 发布退出代码 0；`git diff --check` 无空白错误，允许仅出现仓库已有的 LF/CRLF 提示。

- [ ] **Step 4: 启动本地服务并检查健康状态**

```powershell
$ErrorActionPreference = 'Stop'
& '.\EngineeringManager\.tools\pwsh\pwsh.exe' -NoLogo -NoProfile -File '.\EngineeringManager\scripts\start-local-web.ps1' -Configuration Release -StartupTimeoutSeconds 60
Invoke-WebRequest -Uri 'http://127.0.0.1:5075/health/live' -UseBasicParsing | Select-Object StatusCode,Content
Invoke-WebRequest -Uri 'http://127.0.0.1:5075/health/ready' -UseBasicParsing | Select-Object StatusCode,Content
```

Expected: 两个健康检查均返回 `200 Healthy`。

- [ ] **Step 5: 浏览器检查设置页和关键组件**

使用具备系统管理员权限的现有登录会话，依次检查：

1. `/Admin/Settings`：两张外观卡可预览、保存，刷新后保持；
2. `/Projects`：卡片、筛选、排序、列菜单、分页和项目导出菜单；
3. `/Ledger/External` 与 `/Ledger/Internal`：密集表格保持整齐，无逐格卡片化；
4. `/Employees`、`/Equipment`：快速编辑、输入控件和按钮；
5. `/DataExchange/Export`、`/DataExchange/Import`、`/DataExchange/Tasks`：工作台卡片、下拉和历史表格；
6. 高级筛选、快速编辑、冲突提示和 Toast；
7. 浏览器窄屏模式：侧边栏、全屏编辑弹窗和底部粘性操作区。

每个页面至少切换一次 `Default + RoundedSoft` 和 `LavenderCream + RoundedSoft`；设置页额外确认切回 `Classic` 后恢复现状。

- [ ] **Step 6: 更新部署手册**

在全局显示设置章节补充：

```markdown
- “界面外观”与颜色主题相互独立，可选择“经典紧凑”或“圆润柔光”。
- 旧环境没有 `Display.Appearance` 键时自动使用“经典紧凑”，不需要数据库迁移。
- 外观设置由系统级管理员在“管理中心 → 显示与交互设置”中保存，全站用户统一生效。
```

- [ ] **Step 7: 最终工作区审计**

```powershell
$ErrorActionPreference = 'Stop'
git status --short --branch
git diff --stat
git diff --check
```

将本功能文件与数据往返工作台等已有未提交修改分开列出，禁止把无法识别的用户文件静默纳入提交。

- [ ] **Step 8: 在获得 Git 明确授权后提交文档并按批准方式推送**

```powershell
$ErrorActionPreference = 'Stop'
git add -- 'EngineeringManager/docs/项目部署手册.md' 'EngineeringManager/docs/superpowers/specs/2026-08-03-rounded-soft-appearance-design.md' 'EngineeringManager/docs/superpowers/plans/2026-08-03-rounded-soft-appearance.md'
git commit -m 'docs: document rounded soft appearance mode'
```

推送前再次向用户确认目标分支；未获授权时保留本地改动，不执行 `git push`。

## 计划自检

- 规格中的独立外观维度、经典回退、设置页唯一入口、全局 UI 覆盖、移动端例外和主题组合均有对应任务。
- `UiAppearanceStyle`、`Appearance`、`AppearanceCssClass`、`Display.Appearance` 和 `appearance-rounded-soft` 在所有任务中命名一致。
- 未引入新数据库表、迁移、第三方 CSS 框架或顶部快捷切换。
- 自动测试覆盖应用契约、持久化、权限、HTML、JavaScript、CSS 和布局类；人工检查覆盖密集表格、弹窗与窄屏。
- 所有 Git 操作均保留项目要求的明确授权门槛。
