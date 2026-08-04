# 全站列管理保存与字体清晰度优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (recommended) to implement this plan task-by-task with verification checkpoints.

**Goal:** 让共享列表恢复浏览器中最后一次列管理设置，并移除数据面板悬停时导致文字发虚的面板级变换。

**Architecture:** 保持现有 `localStorage` 状态键和共享 `DataWorkbench` 组件不变，只调整初始化优先级并增加显式保存视图标记；显式保存视图仍可临时覆盖本地状态并在初始化后写回本地。CSS 只取消 `.panel:hover` 的 `transform`，保留指标卡的动效和非变换视觉反馈。

**Tech Stack:** ASP.NET Core Razor Pages、原生 ES modules、CSS、xUnit/FluentAssertions、项目内 .NET/PowerShell 包装脚本。

---

### Task 1: 建立失败回归测试

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

- [x] **Step 1: 添加列设置恢复的失败测试**

在 `DataWorkbenchAssetTests` 中添加：

```csharp
[Fact]
public void WorkbenchPrioritizesLocalColumnsUnlessAViewWasExplicitlySelected()
{
    var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");
    var razor = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml");

    script.Should().Contain("hasExplicitSavedView")
        .And.Contain("persistAfterInit")
        .And.Contain("localColumns");
    razor.Should().Contain("data-current-saved-view-id=\"@Model.CurrentSavedViewId\"");
}
```

- [x] **Step 2: 添加面板悬停清晰度的失败测试**

在 `UiEffectsAssetTests` 中添加：

```csharp
[Fact]
public void DataPanelsDoNotTransformOnHoverWhileMetricCardsKeepMotion()
{
    var css = ReadCss();

    css.Should().NotContain(".panel:hover { transform:")
        .And.Contain(".metric-card:hover { transform:");
}
```

- [x] **Step 3: 运行测试确认是预期失败**

Run from `D:\AI\TZM-project\EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~DataWorkbenchAssetTests|FullyQualifiedName~UiEffectsAssetTests"
```

Expected: FAIL because the current shared Razor/JavaScript has no explicit saved-view marker and `themes.css` still contains `.panel:hover` transforms. The failure must be an assertion failure, not a build or test-discovery error.

### Task 2: 修复列设置初始化优先级

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml:25-32`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/data-table.js:95-102,222-227`

- [x] **Step 1: 暴露显式保存视图标记**

在共享工作台根节点的数据属性中加入：

```cshtml
data-current-saved-view-id="@Model.CurrentSavedViewId"
```

- [x] **Step 2: 实现本地优先、显式服务器视图优先的状态选择**

在 `data-table.js` 中把初始化逻辑调整为以下行为：读取本地状态并检查 `local.columns` 是否为非空数组；普通进入使用本地列，否则回退到服务器列，再回退到默认列；当 `data-current-saved-view-id` 有值且服务器列存在时，使用服务器列并返回 `persistAfterInit: true`。本地状态的 `density` 仍在普通进入时优先，显式保存视图时使用服务器传入的行距。

参考实现结构：

```js
function readLocalState(root) {
  try { return safeParse(localStorage.getItem(storageKey(root)), {}); } catch { return {}; }
}

function initialState(root) {
  const serverColumns = safeParse(root.dataset.savedViewColumns, []);
  const local = readLocalState(root);
  const localColumns = Array.isArray(local.columns) && local.columns.length ? local.columns : null;
  const hasExplicitSavedView = Boolean(root.dataset.currentSavedViewId);
  const useServerColumns = hasExplicitSavedView && serverColumns.length > 0;
  const defaults = safeParse(root.dataset.defaultColumns, []);
  const requestedColumns = useServerColumns ? serverColumns : localColumns ?? serverColumns.length ? serverColumns : defaults;

  return {
    columns: normalizeColumns(root, requestedColumns),
    density: useServerColumns ? root.dataset.rowDensity || local.density || "standard" : local.density || root.dataset.rowDensity || "standard",
    persistAfterInit: useServerColumns
  };
}
```

The conditional expression must be parenthesized in the actual implementation so the intended `useServerColumns ? serverColumns : (localColumns ?? (serverColumns.length ? serverColumns : defaults))` precedence is unambiguous.

- [x] **Step 3: 将显式服务器视图写回本地**

在 `initDataTables` 应用列和行距之后执行：

```js
if (state.persistAfterInit) persist(root);
```

This makes an explicitly selected server view the new local last-used setting without changing ordinary local-first reload behavior.

- [x] **Step 4: 运行列设置回归测试**

Run the same filtered test command from Task 1. Expected: the `DataWorkbenchAssetTests` assertion passes; the CSS test remains red until Task 3.

### Task 3: 移除数据面板悬停变换

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css:354-374`

- [x] **Step 1: 保留指标卡动效，移除面板动效**

拆开当前把 `.metric-card:hover` 与 `.panel:hover` 合并在一起的规则，使 Technology 动效只保留：

```css
body.motion-technology.ui-effects-medium .metric-card:hover { transform: translateY(-2px); box-shadow: var(--app-shadow); }
body.motion-technology.ui-effects-high .metric-card:hover { transform: translateY(-4px); box-shadow: 0 22px 50px rgba(var(--app-primary-rgb), .13); }
```

Apple 高效果只保留 `.metric-card:hover` 的现有缩放/位移；删除 `.panel:hover` 的 `transform`，不删除面板的其他视觉规则。

- [x] **Step 2: 运行悬停 CSS 回归测试**

Run the filtered test command. Expected: both new asset tests pass and existing UI asset tests remain green.

- [x] **Step 3: 检查 JavaScript/CSS 语法与差异**

```powershell
$ErrorActionPreference = 'Stop'
node --check .\src\EngineeringManager.Web\wwwroot\js\components\data-table.js
git diff --check
```

Expected: both commands exit 0; diff only contains the shared workbench, data-table, themes and the two regression test files plus planning artifacts.

### Task 4: 完成测试与构建验证

**Files:**
- No additional production files.

- [x] **Step 1: 运行共享工作台与 UI 资产定向测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~DataWorkbenchAssetTests|FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~ProjectListExportPageTests"
```

- [x] **Step 2: 运行完整测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release
```

Expected: exit code 0 and no failed tests.

- [x] **Step 3: 运行 Release 构建**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release --no-restore /p:UseSharedCompilation=false
```

Expected: exit code 0 and no compilation errors.

### Task 5: 重启服务并做运行态验收

**Files:**
- No additional files.

- [x] **Step 1: 用正确项目根目录重启本地 Web 服务**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\start-local-web.ps1
```

Run from `D:\AI\TZM-project\EngineeringManager`; expected URL is `http://127.0.0.1:5075`.

- [x] **Step 2: 检查健康端点和静态资源**

```powershell
$ErrorActionPreference = 'Stop'
(Invoke-WebRequest -Uri 'http://127.0.0.1:5075/health/live' -UseBasicParsing).StatusCode
(Invoke-WebRequest -Uri 'http://127.0.0.1:5075/health/ready' -UseBasicParsing).StatusCode
```

Expected: both are 200; CSS/JS responses contain the new versioned assets after a hard refresh.

- [ ] **Step 3: 浏览器检查悬停清晰度和列设置记忆**

在项目管理列表页移动鼠标进入/离开主数据面板、表格和指标卡，确认表格正文不再发虚且页面不跳动；打开列管理改变顺序和显隐，点击确认后刷新并重新进入页面，确认本地最后一次设置恢复；选择一次服务器保存视图后再次打开，确认该次选择成为新的本地最后设置。无法登录时记录登录阻塞，不猜测或重置密码。

运行记录：自动化测试、构建、服务健康检查和运行态 CSS 资源检查已完成；内置浏览器当前只有登录页，没有可用登录态，因此项目列表上的真实鼠标/列管理操作留待用户登录后复核。

- [x] **Step 4: 检查工作区差异并准备交付**

使用 `git status --short` 和 `git diff --check` 区分本轮修改与已有未提交文件；不清理、不回滚、不提交未获用户确认的文件。完成后向用户报告测试、构建、服务和浏览器验收结果。
