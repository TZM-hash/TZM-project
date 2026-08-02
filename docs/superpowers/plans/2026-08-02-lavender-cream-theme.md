# 薰衣草奶油全局主题 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有两套显示主题之外新增可预览、可保存并全站生效的“薰衣草奶油”主题，同时保持现有业务行为与主题兼容。

**Architecture:** 扩展现有 `VisualTheme` 设置契约，继续把主题名称存入现有 `Display.Theme` 键；由布局输出新的 body class，再通过 `themes.css` 中的全局令牌和少量共享组件覆盖完成全站换肤。设置页沿用当前卡片式单选和 JavaScript 即时预览，不新增依赖、路由或数据库迁移。

**Tech Stack:** ASP.NET Core Razor Pages、C# records/enums、CSS custom properties、原生 ES modules、xUnit、FluentAssertions、SQLite 测试夹具。

---

## 文件职责

- `EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs`：声明新主题值并把它映射为 CSS class。
- `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml`：显示第三张主题选择卡。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js`：在即时预览时正确移除和添加第三个主题 class。
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`：定义新主题令牌、应用外壳和共享组件样式。
- `EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`：验证 CSS class 映射、保存、读取和现有键值存储。
- `EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs`：验证设置页渲染第三个主题选项。
- `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`：验证新主题 CSS 与预览 JavaScript 资源完整。

### Task 1: 扩展主题契约与持久化验证

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs`

- [ ] **Step 1: 编写失败测试**

在 `SystemSettingsServiceTests` 中增加：

```csharp
[Fact]
public async Task LavenderCreamThemeMapsToCssClassAndPersistsInExistingSetting()
{
    await using var fixture = await Fixture.CreateAsync();
    var requested = SystemDisplaySettings.Default with { Theme = VisualTheme.LavenderCream };

    requested.ThemeCssClass.Should().Be("theme-lavender-cream");

    await fixture.Service.SaveAsync(new SettingsActor("sys", "系统管理员", true), requested, default);

    (await fixture.Service.GetAsync(default)).Theme.Should().Be(VisualTheme.LavenderCream);
    var stored = await fixture.Db.SystemSettings.SingleAsync(item => item.Key == "Display.Theme");
    stored.Value.Should().Be("LavenderCream");
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~SystemSettingsServiceTests' --no-restore
```

Expected: 编译失败，提示 `VisualTheme` 不包含 `LavenderCream`。

- [ ] **Step 3: 实现最小主题契约**

把主题 enum 和映射改为：

```csharp
public enum VisualTheme
{
    Default = 1,
    ClearGlass = 2,
    LavenderCream = 3
}

public string ThemeCssClass => Theme switch
{
    VisualTheme.ClearGlass => "theme-clear-glass",
    VisualTheme.LavenderCream => "theme-lavender-cream",
    _ => "theme-default"
};
```

保持 `Default = 1` 与 `ClearGlass = 2` 数值不变，避免已有保存值产生兼容问题。

- [ ] **Step 4: 运行测试并确认通过**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~SystemSettingsServiceTests' --no-restore
```

Expected: `SystemSettingsServiceTests` 全部通过。

- [ ] **Step 5: 提交检查点（仅在用户确认后执行）**

```powershell
$ErrorActionPreference = 'Stop'
git add -- EngineeringManager/src/EngineeringManager.Application/Settings/SystemSettingsDtos.cs EngineeringManager/tests/EngineeringManager.Tests/Application/SystemSettingsServiceTests.cs
git commit -m 'feat: 扩展薰衣草奶油主题设置'
```

### Task 2: 增加设置页选项与即时预览

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js`

- [ ] **Step 1: 编写失败测试**

在 `SystemAdministratorCanEditAllConfirmedDisplaySettings` 中补充：

```csharp
html.Should().Contain("value=\"LavenderCream\"")
    .And.Contain("data-theme-option=\"theme-lavender-cream\"")
    .And.Contain("薰衣草奶油");
```

在 `AssetsContainConfirmedThemesEffectsAndReducedMotion` 中补充：

```csharp
js.Should().Contain("\"theme-lavender-cream\"");
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~SystemSettingsPageTests|FullyQualifiedName~UiEffectsAssetTests' --no-restore
```

Expected: 设置页 HTML 与 JavaScript class 列表断言失败。

- [ ] **Step 3: 增加第三张主题卡**

在“清透毛玻璃”卡片之后加入：

```html
<label class="option-card" data-theme-option="theme-lavender-cream">
    <input asp-for="Input.Theme" type="radio" value="LavenderCream" disabled="@Model.IsReadOnly" />
    <span class="option-preview option-preview--lavender" aria-hidden="true"><i></i><i></i><i></i></span>
    <span><strong>薰衣草奶油</strong><small>柔和紫白、奶油卡片和清新的多彩点缀。</small></span>
</label>
```

- [ ] **Step 4: 扩展预览 class 列表**

把 `settings.js` 第一行改为：

```javascript
const themeClasses = ["theme-default", "theme-clear-glass", "theme-lavender-cream"];
```

现有 `swapClass` 和 `initThemePreview` 保持不变。

- [ ] **Step 5: 运行测试并确认页面与脚本通过**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~SystemSettingsPageTests|FullyQualifiedName~UiEffectsAssetTests' --no-restore
```

Expected: 当前任务相关页面与 JavaScript 断言通过；CSS 新主题断言将在 Task 3 添加。

- [ ] **Step 6: 提交检查点（仅在用户确认后执行）**

```powershell
$ErrorActionPreference = 'Stop'
git add -- EngineeringManager/src/EngineeringManager.Web/Pages/Admin/Settings/Index.cshtml EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/settings.js EngineeringManager/tests/EngineeringManager.Tests/Web/SystemSettingsPageTests.cs EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs
git commit -m 'feat: 增加薰衣草主题选择与预览'
```

### Task 3: 实现全局主题令牌与共享组件材质

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`

- [ ] **Step 1: 编写失败的主题资源断言**

在 `AssetsContainConfirmedThemesEffectsAndReducedMotion` 中补充：

```csharp
css.Should().Contain("body.theme-lavender-cream")
    .And.Contain("--app-primary: #7653d6")
    .And.Contain(".option-preview--lavender")
    .And.Contain("body.theme-lavender-cream .app-sidebar")
    .And.Contain("body.theme-lavender-cream .data-table th");
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~UiEffectsAssetTests' --no-restore
```

Expected: CSS 不包含 `body.theme-lavender-cream`，测试失败。

- [ ] **Step 3: 写入完整主题样式**

在 `themes.css` 的 `theme-clear-glass` 区块之后、动效区块之前加入：

```css
body.theme-lavender-cream {
  --app-bg: #f5f1ff;
  --app-surface: rgba(255, 252, 255, .9);
  --app-surface-raised: rgba(251, 247, 255, .94);
  --app-surface-soft: #f0e9ff;
  --app-text: #2d2740;
  --app-text-soft: #514a67;
  --app-muted: #756d88;
  --app-border: #e4dcf1;
  --app-border-strong: #d2c5e8;
  --app-primary: #7653d6;
  --app-primary-hover: #6241bd;
  --app-primary-soft: #eee7ff;
  --app-accent: #58bea3;
  --app-success: #298c71;
  --app-warning: #c97858;
  --app-danger: #c94f72;
  --app-info: #4b9ed6;
  --app-radius-sm: 10px;
  --app-radius-md: 14px;
  --app-radius-lg: 20px;
  --app-shadow-soft: 0 7px 20px rgba(91, 67, 137, .08);
  --app-shadow: 0 22px 52px rgba(91, 67, 137, .14);
  color-scheme: light;
  background:
    radial-gradient(circle at 12% 9%, rgba(199, 178, 255, .34), transparent 31%),
    radial-gradient(circle at 88% 18%, rgba(255, 197, 224, .22), transparent 27%),
    radial-gradient(circle at 78% 86%, rgba(160, 231, 211, .18), transparent 30%),
    linear-gradient(145deg, #f6f1ff 0%, #fffafd 48%, #f1efff 100%);
}

body.theme-lavender-cream .app-bg-fx {
  background-image:
    linear-gradient(rgba(118, 83, 214, .025) 1px, transparent 1px),
    linear-gradient(90deg, rgba(118, 83, 214, .025) 1px, transparent 1px);
}
body.theme-lavender-cream .app-bg-fx span:nth-child(1) { background: #c6adff; }
body.theme-lavender-cream .app-bg-fx span:nth-child(2) { background: #f4b8d5; }
body.theme-lavender-cream .app-bg-fx span:nth-child(3) { background: #8edfc8; }

body.theme-lavender-cream .app-sidebar {
  border-color: rgba(255, 255, 255, .48);
  background: linear-gradient(180deg, rgba(202, 181, 252, .97), rgba(151, 116, 232, .98));
  box-shadow: 12px 0 38px rgba(89, 61, 150, .17), inset -1px 0 0 rgba(255, 255, 255, .22);
}
body.theme-lavender-cream .app-sidebar-brand { border-color: rgba(255, 255, 255, .22); color: #fff; }
body.theme-lavender-cream .brand-mark { background: linear-gradient(135deg, #ff9fc5, #8f6ee8 58%, #70d2b6); box-shadow: 0 12px 26px rgba(94, 56, 167, .25); }
body.theme-lavender-cream .nav-group-label { color: rgba(255, 255, 255, .65); }
body.theme-lavender-cream .nav-link { color: rgba(255, 255, 255, .88); }
body.theme-lavender-cream .nav-link:hover { color: #fff; background: rgba(255, 255, 255, .14); }
body.theme-lavender-cream .nav-link.is-active { color: #fff; background: rgba(255, 255, 255, .25); box-shadow: 0 10px 24px rgba(78, 45, 143, .18), inset 0 0 0 1px rgba(255, 255, 255, .18); }
body.theme-lavender-cream .nav-link.is-active::before { background: #fff; }
body.theme-lavender-cream .sidebar-footer { border-color: rgba(255, 255, 255, .2); color: rgba(255, 255, 255, .72); }

body.theme-lavender-cream .app-header {
  border-color: rgba(218, 207, 235, .78);
  background: rgba(255, 252, 255, .78);
  backdrop-filter: blur(22px) saturate(145%);
  box-shadow: 0 8px 28px rgba(91, 67, 137, .07);
}
body.theme-lavender-cream .menu-toggle,
body.theme-lavender-cream .header-icon-button {
  border-color: rgba(210, 197, 232, .9);
  background: rgba(255, 253, 255, .8);
}
body.theme-lavender-cream .account-avatar { background: linear-gradient(135deg, #8d6be5, #db83b1); }

body.theme-lavender-cream .panel,
body.theme-lavender-cream .metric-card,
body.theme-lavender-cream .form-section,
body.theme-lavender-cream .data-work-surface,
body.theme-lavender-cream .settings-select-card,
body.theme-lavender-cream .table-wrap,
body.theme-lavender-cream .data-table-wrap {
  border-color: rgba(225, 216, 239, .92);
  background: rgba(255, 252, 255, .84);
  backdrop-filter: blur(18px) saturate(135%);
  box-shadow: var(--app-shadow-soft), inset 0 1px 0 rgba(255, 255, 255, .72);
}
body.theme-lavender-cream .metric-card::after { background: linear-gradient(90deg, #8c6be4, #e58bb5, #65cdb0); }
body.theme-lavender-cream .progress-fill { background: linear-gradient(90deg, #8060dc, #69c8b0); }

body.theme-lavender-cream .button--primary {
  color: #fff;
  background: linear-gradient(105deg, #7653d6, #9a72e5 62%, #bf7dcb);
  box-shadow: 0 10px 22px rgba(103, 70, 185, .24);
}
body.theme-lavender-cream .button--primary:hover { color: #fff; background: #6241bd; }
body.theme-lavender-cream .button--secondary { color: #6545bc; border-color: #d4c6ee; background: #f7f2ff; }
body.theme-lavender-cream .button--secondary:hover { border-color: #8b6ada; background: #eee7ff; }
body.theme-lavender-cream .button--danger { color: #a53f60; border-color: #efbfd0; background: #fff1f6; }

body.theme-lavender-cream :focus-visible { outline-color: rgba(118, 83, 214, .38); }
body.theme-lavender-cream .form-grid input:focus,
body.theme-lavender-cream .form-grid select:focus,
body.theme-lavender-cream .form-grid textarea:focus,
body.theme-lavender-cream .filter-bar input:focus,
body.theme-lavender-cream .filter-bar select:focus,
body.theme-lavender-cream .settings-select-card select:focus { box-shadow: 0 0 0 3px rgba(118, 83, 214, .13); }
body.theme-lavender-cream .option-card:has(input:checked) { box-shadow: 0 0 0 3px rgba(118, 83, 214, .11); }

body.theme-lavender-cream .data-table th,
body.theme-lavender-cream .table-wrap > table th { color: #5d5471; background: rgba(241, 235, 250, .94); }
body.theme-lavender-cream .data-table th,
body.theme-lavender-cream .data-table td,
body.theme-lavender-cream .table-wrap > table th,
body.theme-lavender-cream .table-wrap > table td { border-bottom-color: rgba(220, 210, 235, .78); }
body.theme-lavender-cream .data-table tbody tr:hover td,
body.theme-lavender-cream .table-wrap > table tbody tr:hover td { background: rgba(244, 238, 253, .78); }

body.theme-lavender-cream .pill--success,
body.theme-lavender-cream .alert--success { color: #236f5c; border-color: #aee0d1; background: #eaf8f3; }
body.theme-lavender-cream .pill--warning { color: #9b6049; background: #ffeadf; }
body.theme-lavender-cream .pill--danger { color: #a33d5c; background: #ffe6ef; }
body.theme-lavender-cream .alert--info { color: #356f9a; border-color: #bddcf0; background: #eef8ff; }
body.theme-lavender-cream .navigation-pending-indicator,
body.theme-lavender-cream .toast-region,
body.theme-lavender-cream .conflict-notice { background: rgba(255, 252, 255, .94); }

.option-preview--lavender {
  background: linear-gradient(135deg, #d8c8ff, #f9ecff 55%, #dff8ef);
}
.option-preview--lavender i { background: rgba(255, 252, 255, .82); box-shadow: 0 5px 12px rgba(91, 67, 137, .11), inset 0 0 0 1px rgba(255, 255, 255, .7); }
.option-preview--lavender i:first-child { background: linear-gradient(180deg, #b89aef, #8c6bd8); }
```

- [ ] **Step 4: 运行主题资源测试并确认通过**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~UiEffectsAssetTests' --no-restore
```

Expected: `UiEffectsAssetTests` 全部通过。

- [ ] **Step 5: 提交检查点（仅在用户确认后执行）**

```powershell
$ErrorActionPreference = 'Stop'
git add -- EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs
git commit -m 'feat: 实现薰衣草奶油全局样式'
```

### Task 4: 回归验证、视觉检查与服务交付

**Files:**
- Verify: `EngineeringManager/EngineeringManager.sln`
- Verify: `EngineeringManager/src/EngineeringManager.Web`

- [ ] **Step 1: 运行主题相关定向测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter 'FullyQualifiedName~SystemSettingsServiceTests|FullyQualifiedName~SystemSettingsPageTests|FullyQualifiedName~UiEffectsAssetTests' --no-restore
```

Expected: 所有筛选出的测试通过，失败数为 0。

- [ ] **Step 2: 运行完整测试与 Release 构建**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\EngineeringManager\EngineeringManager.sln --no-restore
dotnet build .\EngineeringManager\EngineeringManager.sln -c Release --no-restore
```

Expected: 完整测试失败数为 0；Release 构建警告与错误为 0，或只保留任务开始前已存在且有记录的警告。

- [ ] **Step 3: 检查补丁与任务边界**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: 无空白错误；本次仅新增或修改计划列出的主题文件，原有列表排序工作区改动保持未覆盖状态。

- [ ] **Step 4: 启动或重启本地服务**

先解析并停止仅监听 `5075` 的现有项目进程，再以隐藏窗口启动：

```powershell
$ErrorActionPreference = 'Stop'
$listener = Get-NetTCPConnection -LocalPort 5075 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    Stop-Process -Id $listener.OwningProcess
}
$projectPath = 'D:\AI\TZM-project\EngineeringManager\src\EngineeringManager.Web\EngineeringManager.Web.csproj'
Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $projectPath, '--configuration', 'Release', '--no-build', '--urls', 'http://127.0.0.1:5075') -WorkingDirectory 'D:\AI\TZM-project\EngineeringManager' -WindowStyle Hidden
```

随后验证：

```powershell
$ErrorActionPreference = 'Stop'
$response = $null
for ($attempt = 1; $attempt -le 30 -and $null -eq $response; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri 'http://127.0.0.1:5075/Identity/Account/Login' -UseBasicParsing
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}
if ($null -eq $response) { throw '本地服务未在预期时间内启动。' }
$response | Select-Object StatusCode
```

Expected: HTTP `200`。

- [ ] **Step 5: 完成视觉验收**

在用户已选择的浏览器中，以同一桌面视口完成以下检查：

1. 登录页加载正常，无布局溢出。
2. 系统管理员进入 `/Admin/Settings`，选择“薰衣草奶油”后立即看到紫色侧栏、奶油卡片和多彩点缀。
3. 保存并打开 `/Projects`，确认主题在刷新后保持。
4. 打开一个含表格、表单和弹窗的代表页面，检查表头、hover、focus、状态色和弹窗对比度。
5. 对照参考图与实现截图，修复明显的 P0/P1/P2 视觉差异；不复制短视频文字和悬浮头像层。

- [ ] **Step 6: 准备代码提交计划**

列出本次主题文件与所有既有未识别改动，向用户请求一次性提交确认。不得自动包含原有列表排序文件，也不得推送远端。

---

## 实施约束

- 直接在当前工作区执行，不创建 worktree，也不使用子代理。
- 编辑文件统一使用 `apply_patch`。
- 不增加依赖、不创建 Migration、不改数据库结构。
- 不覆盖当前工作区已有的列表排序与中央账本相关修改。
- 视觉检查若受登录阻挡，停在登录页并请用户登录；不绕过认证或修改账号数据。
