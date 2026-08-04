# 全局字体可读性增强实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 在不改变页面结构的前提下，提高项目列表、项目详情及其他页面的文字可读性，并保持长文本与宽表格不溢出。

**Architecture:** 保留现有全局字体设置入口和四档字号 class，仅把默认字号档位整体上调约 1–2px；同步提高共享表格、工具栏、指标卡与项目详情摘要中目前过小的显式 rem 字号。对表格继续使用横向滚动，对项目名称、承包商和详情字段保留 ellipsis/wrap 规则，不改动数据、布局网格或交互逻辑。

**Tech Stack:** ASP.NET Core Razor Pages、CSS、xUnit/FluentAssertions、PowerShell 包装脚本。

---

### Task 1: 建立字号回归测试

**Files:**
- Modify: `D:/AI/TZM-project/EngineeringManager/tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

- [x] **Step 1: 写失败测试**

新增一个资产测试，要求 CSS 同时包含新的全局字号档位、共享表格字号、项目列表字号和详情摘要字号，并要求表格仍保留横向溢出容器：

```csharp
[Fact]
public void GlobalFontScaleImprovesReadabilityWithoutRemovingOverflowGuards()
{
    var css = ReadCss();

    css.Should().Contain("html.font-size-standard { font-size: 17px; }")
        .And.Contain("html.font-size-large { font-size: 19px; }")
        .And.Contain(".data-table, .table-wrap > table { width: 100%; min-width: 44rem; border-collapse: separate; border-spacing: 0; font-size: .88rem; }")
        .And.Contain("#projects-table .data-table th, #projects-table .data-table td")
        .And.Contain(".project-summary-grid dt")
        .And.Contain(".table-wrap, .data-table-wrap { width: 100%; margin-top: 1rem; overflow-x: auto;");
}
```

- [x] **Step 2: 运行定向测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~UiEffectsAssetTests.GlobalFontScaleImprovesReadabilityWithoutRemovingOverflowGuards"
```

预期：测试因新的字号断言尚不存在而失败，且不是编译或测试发现错误。

### Task 2: 调整全局与共享组件字号

**Files:**
- Modify: `D:/AI/TZM-project/EngineeringManager/src/EngineeringManager.Web/wwwroot/css/base.css`
- Modify: `D:/AI/TZM-project/EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `D:/AI/TZM-project/EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`

- [x] **Step 1: 上调全局字号档位**

将 `base.css` 的字号 class 调整为 `small=15px`、`standard=17px`、`large=19px`、`extra-large=21px`，保留现有设置页面与 class 名称，避免改变持久化设置契约。

- [x] **Step 2: 放大共享文本与数据表格**

在 `components.css` 提高共享 `.data-table` 正文和表头字号，并适度提高工具栏、分页、筛选项、指标标签、按钮和状态 pill 等小号文本；不删除 `.table-wrap`/`.data-table-wrap` 的 `overflow-x: auto`，不改变表格列结构。

- [x] **Step 3: 保留行距选项并提高紧凑档可读性**

在 `themes.css` 中只提高 `table-density-compact` 的文字下限，不改变 standard/spacious 的布局语义和行距选择。

- [x] **Step 4: 运行定向测试确认通过**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~UiEffectsAssetTests"
```

预期：字号回归测试与现有 UI 资产测试全部通过。

### Task 3: 调整项目管理与项目详情页字号

**Files:**
- Modify: `D:/AI/TZM-project/EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [x] **Step 1: 放大项目列表表格文本**

为 `#projects-table` 的 `th/td` 增加明确的字号与行高，项目名称、承包商仍使用现有省略规则，进度列继续保留最小宽度。

- [x] **Step 2: 放大项目详情摘要与财务信息**

提高 `.project-summary-grid` 标签和值、项目顶部概览 strip、相关项目卡和活动提示中的小字号；对备注、自由文本和详情表格保留 `white-space`、`overflow-wrap`、`text-overflow` 现有防溢出规则。

- [x] **Step 3: 运行项目页面资产测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~UiEffectsAssetTests|FullyQualifiedName~ProjectCollectionEntryPageTests|FullyQualifiedName~ProjectDetailsPageTests"
```

预期：所有匹配测试通过；若项目详情测试名称不同，使用 `--list-tests` 找到实际名称后按同一测试集运行。

### Task 4: 完整验证与服务重启

**Files:**
- No additional production files.

- [x] **Step 1: 检查 CSS/差异**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
```

- [x] **Step 2: 运行全量测试和 Release 构建**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release --no-restore /p:UseSharedCompilation=false
```

- [x] **Step 3: 重启并验收本地服务**

从 `D:/AI/TZM-project/EngineeringManager` 运行 `scripts/start-local-web.ps1`，确认 `http://127.0.0.1:5075/health/live` 与 `/health/ready` 返回 200，并确认 CSS 资源正常返回。

- [x] **Step 4: 检查前端资源与工作区**

```powershell
$ErrorActionPreference = 'Stop'
node --check .\src\EngineeringManager.Web\wwwroot\js\components\data-table.js
git diff --check
```

不提交、不回滚其他已有未提交改动。
