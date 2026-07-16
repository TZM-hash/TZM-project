# 工程项目经营管理系统阶段 0 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `D:\\AI\\TZM-project\\EngineeringManager` 建立可运行、可测试、可部署到 IIS 的新项目基线，为后续业务模块提供稳定的 ASP.NET Core、SQL Server、Identity、响应式 UI、PWA、附件、日志和健康检查基础。

**Architecture:** 新项目采用模块化单体。Presentation 使用 ASP.NET Core Razor Pages，并保留阶段成果离线同步所需的轻量 API 入口；Application、Domain、Infrastructure 和 Tests 分层。阶段 0 不实现项目、财务、工资等业务模块，只建立后续模块会复用的技术边界和质量门禁。

**Tech Stack:** .NET 10 LTS、ASP.NET Core Razor Pages、EF Core SQL Server、ASP.NET Core Identity、xUnit、FluentAssertions、SQLite 测试数据库、原生 CSS/JavaScript、PWA Manifest/Service Worker、Windows Server + IIS。

**Working directory assumption:** `D:\\AI\\TZM-project\\EngineeringManager`。如果用户在执行前指定其他项目名称或目录，只替换本计划中的该根路径，不改变模块边界和任务顺序。

---

## 执行前置条件

以下信息在真正创建项目和 Git 仓库前确认：

- 新项目目录是否采用 `EngineeringManager`。
- 是否现在初始化本地 Git 仓库。
- 远程 Git 地址（用户尚未提供时只创建本地仓库，不配置 remote）。
- 本地 SQL Server 实例名称和数据库名称；默认建议 `localhost\\SQLEXPRESS` 与 `EngineeringManager`。
- 初始管理员账号是否沿用临时开发账号；生产密码不得写入仓库。

执行前必须读取：

- `D:\\AI\\TZM-project\\docs\\superpowers\\specs\\2026-07-16-engineering-management-system-design.md`
- `D:\\AI\\TZM-project\\docs\\开发进度.md`

执行过程中只更新同一个进度文件 `D:\\AI\\TZM-project\\docs\\开发进度.md`。

## 文件地图

阶段 0 预计创建以下文件和目录：

```text
EngineeringManager/
  .gitignore
  global.json
  Directory.Build.props
  EngineeringManager.sln
  README.md
  scripts/
    dotnet.ps1
    quality-gate.ps1
  src/
    EngineeringManager.Domain/
      EngineeringManager.Domain.csproj
    EngineeringManager.Application/
      EngineeringManager.Application.csproj
    EngineeringManager.Infrastructure/
      EngineeringManager.Infrastructure.csproj
      Data/ApplicationDbContext.cs
      Data/ApplicationUser.cs
      Files/IFileStore.cs
      Files/LocalFileStore.cs
    EngineeringManager.Web/
      EngineeringManager.Web.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
      Pages/Index.cshtml
      Pages/Index.cshtml.cs
      Pages/Shared/_Layout.cshtml
      Pages/Shared/_ValidationScriptsPartial.cshtml
      Pages/Shared/Error.cshtml
      wwwroot/css/base.css
      wwwroot/css/components.css
      wwwroot/css/pages.css
      wwwroot/css/themes.css
      wwwroot/js/site.js
      wwwroot/manifest.webmanifest
      wwwroot/service-worker.js
      Properties/launchSettings.json
  tests/
    EngineeringManager.Tests/
      EngineeringManager.Tests.csproj
      Infrastructure/ApplicationDbContextTests.cs
      Infrastructure/LocalFileStoreTests.cs
      Web/HealthEndpointTests.cs
      Web/HomePageTests.cs
```

每个文件只承担一个清晰职责：Web 项目负责页面和启动，Infrastructure 负责数据库/文件存储，Domain/Application 先提供空的业务边界，Tests 负责阶段 0 的基线回归。

## Task 1: 建立 .NET 10 SDK 和仓库级基线

**Files:**
- Create: `EngineeringManager/global.json`
- Create: `EngineeringManager/Directory.Build.props`
- Create: `EngineeringManager/.gitignore`
- Create: `EngineeringManager/scripts/dotnet.ps1`
- Create: `EngineeringManager/README.md`
- Modify: `docs/开发进度.md`

- [ ] **Step 1: 写 SDK 版本基线文件**

创建 `global.json`，固定 .NET 10 SDK，避免不同机器使用不同 SDK：

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 2: 写仓库级编译属性**

创建 `Directory.Build.props`，统一启用可空引用、隐式 using、分析器和 UTF-8：

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <LangVersion>latestMajor</LangVersion>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 写本地 SDK 包装脚本**

创建 `scripts/dotnet.ps1`，优先使用仓库内 `.dotnet\\dotnet.exe`，否则使用 PATH 中的 dotnet；找不到时立即失败：

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
  $localDotnet
} else {
  (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}
if ([string]::IsNullOrWhiteSpace($dotnet)) {
  throw 'dotnet executable was not found. Install .NET 10 SDK or restore the local .dotnet folder.'
}
& $dotnet @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

- [ ] **Step 4: 写仓库忽略规则**

`.gitignore` 至少忽略 `.dotnet/`、`bin/`、`obj/`、`.vs/`、`TestResults/`、`App_Data/`、本地密钥、备份包和附件文件。

- [ ] **Step 5: 验证 SDK 基线**

Run from `D:\\AI\\TZM-project\\EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 --version
```

Expected: 输出 `10.0.100` 或同一 feature band 的 `10.0.1xx`。如果机器没有 .NET 10，先执行以下安装命令，再运行同一命令确认，不修改系统全局 SDK：

```powershell
$ErrorActionPreference = 'Stop'
$installer = Join-Path $env:TEMP 'engineering-manager-dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
pwsh -File $installer -Version 10.0.100 -InstallDir (Join-Path (Get-Location) '.dotnet')
```

- [ ] **Step 6: 更新进度文档**

在 `docs/开发进度.md` 的“当前状态”和“阶段历史”写明 SDK 版本、安装位置和验证命令。

## Task 2: 创建解决方案和分层项目

**Files:**
- Create: `EngineeringManager/EngineeringManager.sln`
- Create: `EngineeringManager/src/EngineeringManager.Domain/EngineeringManager.Domain.csproj`
- Create: `EngineeringManager/src/EngineeringManager.Application/EngineeringManager.Application.csproj`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/EngineeringManager.Infrastructure.csproj`
- Create: `EngineeringManager/src/EngineeringManager.Web/EngineeringManager.Web.csproj`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj`
- Modify: `EngineeringManager/EngineeringManager.sln`

- [ ] **Step 1: 创建解决方案和项目**

Run:

```powershell
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path .\\src, .\\tests | Out-Null
pwsh -File .\\scripts\\dotnet.ps1 new sln -n EngineeringManager
pwsh -File .\\scripts\\dotnet.ps1 new classlib -n EngineeringManager.Domain -o .\\src\\EngineeringManager.Domain --framework net10.0
pwsh -File .\\scripts\\dotnet.ps1 new classlib -n EngineeringManager.Application -o .\\src\\EngineeringManager.Application --framework net10.0
pwsh -File .\\scripts\\dotnet.ps1 new classlib -n EngineeringManager.Infrastructure -o .\\src\\EngineeringManager.Infrastructure --framework net10.0
pwsh -File .\\scripts\\dotnet.ps1 new webapp -n EngineeringManager.Web -o .\\src\\EngineeringManager.Web --framework net10.0 --auth None
pwsh -File .\\scripts\\dotnet.ps1 new xunit -n EngineeringManager.Tests -o .\\tests\\EngineeringManager.Tests --framework net10.0
```

- [ ] **Step 2: 添加项目引用**

Run:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 sln .\\EngineeringManager.sln add .\\src\\EngineeringManager.Domain\\EngineeringManager.Domain.csproj .\\src\\EngineeringManager.Application\\EngineeringManager.Application.csproj .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Application\\EngineeringManager.Application.csproj reference .\\src\\EngineeringManager.Domain\\EngineeringManager.Domain.csproj
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj reference .\\src\\EngineeringManager.Domain\\EngineeringManager.Domain.csproj .\\src\\EngineeringManager.Application\\EngineeringManager.Application.csproj
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj reference .\\src\\EngineeringManager.Domain\\EngineeringManager.Domain.csproj .\\src\\EngineeringManager.Application\\EngineeringManager.Application.csproj .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj
pwsh -File .\\scripts\\dotnet.ps1 add .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj reference .\\src\\EngineeringManager.Domain\\EngineeringManager.Domain.csproj .\\src\\EngineeringManager.Application\\EngineeringManager.Application.csproj .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj
```

- [ ] **Step 3: 清理模板内容并保留可运行入口**

将三个类库中的 `Class1.cs` 内容替换为对应命名空间的空程序集标记类；将 Web 模板首页和 Privacy 页改为简体中文基线页面。由于 Web 项目使用 `--auth None`，不生成 SQLite 模板数据库和默认 Identity 页面，Identity UI 在 Task 3 中统一接入。

- [ ] **Step 4: 还原并构建空解决方案**

Run:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 restore .\\EngineeringManager.sln
pwsh -File .\\scripts\\dotnet.ps1 build .\\EngineeringManager.sln --configuration Release --no-restore
```

Expected: `Build succeeded`，错误数为 0。

## Task 3: 建立 SQL Server、Identity 和健康检查基线

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/EngineeringManager.Infrastructure.csproj`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationUser.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/appsettings.json`
- Modify: `EngineeringManager/src/EngineeringManager.Web/appsettings.Development.json`
- Create: `EngineeringManager/src/EngineeringManager.Web/Properties/launchSettings.json`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/ApplicationDbContextTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/HealthEndpointTests.cs`

- [ ] **Step 1: 添加明确版本的数据库包**

添加与 .NET 10 兼容的稳定版本，统一使用 `10.0.0`；FluentAssertions 使用 `8.0.0`：

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 10.0.0
pwsh -File .\\scripts\\dotnet.ps1 add .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj package FluentAssertions --version 8.0.0
```

执行 `dotnet restore` 后检查所有包版本；后续阶段不得无理由升级这些基础包。

- [ ] **Step 2: 建立应用用户和上下文**

`ApplicationUser` 只保留阶段 0 需要的账号字段：`Id`、`UserName`、`Email`、`DisplayName`、`IsEnabled`、`CreatedAt`。不要提前加入项目、工资或权限业务字段。

`ApplicationDbContext` 继承 `IdentityDbContext<ApplicationUser>`，只配置 SQL Server provider 和统一命名约定；业务实体在后续阶段逐步加入。

- [ ] **Step 3: 配置启动和连接字符串**

`Program.cs` 必须完成：

- Razor Pages。
- Identity Cookie 认证。
- `ApplicationDbContext` 的 SQL Server 注册。
- `/health/live`：只检查进程可响应。
- `/health/ready`：检查数据库连接。
- 开发环境异常页，生产环境统一异常处理。
- 静态资源、认证、授权和页面路由。

`appsettings.Development.json` 使用本地开发连接字符串；生产连接字符串只从环境变量或 IIS 配置读取，不提交密码。

- [ ] **Step 4: 写数据库模型和健康端点测试**

`ApplicationDbContextTests` 使用 SQLite 内存数据库确认 Identity 模型可以创建；`HealthEndpointTests` 使用 `WebApplicationFactory<Program>` 确认 `/health/live` 返回 200，并且未配置数据库时 `/health/ready` 返回明确的非成功状态而不是抛出未处理异常。

Run:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 test .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj --configuration Release
```

Expected: 测试通过，失败数为 0。

- [ ] **Step 5: 创建第一版 EF Migration**

在本地 SQL Server 连接可用后运行：

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 ef migrations add InitialIdentity --project .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj --startup-project .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj --output-dir Data\\Migrations
pwsh -File .\\scripts\\dotnet.ps1 ef database update --project .\\src\\EngineeringManager.Infrastructure\\EngineeringManager.Infrastructure.csproj --startup-project .\\src\\EngineeringManager.Web\\EngineeringManager.Web.csproj
```

Expected：数据库创建成功，Identity 表存在；迁移名称写入进度文档。

## Task 4: 建立响应式 UI 外壳和简体中文基础样式

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/base.css`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/themes.css`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] **Step 1: 定义设计变量和应用外壳**

`base.css` 定义背景、文字、边框、主色、风险色、间距、圆角和焦点样式；`components.css` 定义导航、卡片、指标卡、表格、按钮、空状态和提示条；`pages.css` 定义首页和响应式断点；`themes.css` 定义动效等级和 `prefers-reduced-motion`。

页面使用简体中文，导航先放置已确认的业务区域入口和“阶段 0 基线”状态，不伪造尚未实现的业务数据。

- [ ] **Step 2: 重写布局和首页**

`_Layout.cshtml` 提供：

- 响应式侧边栏/顶部导航。
- 用户菜单和登录状态。
- 主内容区。
- 全局提醒容器。
- PWA 安装入口。

`Index.cshtml` 展示基线状态卡：系统运行状态、数据库连接状态、当前阶段、进度文档入口和“业务模块将在后续阶段启用”的空状态。

- [ ] **Step 3: 添加首页测试**

`HomePageTests` 使用 `WebApplicationFactory<Program>` 请求 `/`，断言：状态码 200、响应包含简体中文标题、包含 `viewport` meta、引用 `base.css`，且没有引用外部 CDN。

- [ ] **Step 4: 验证响应式静态资源**

Run:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\dotnet.ps1 test .\\tests\\EngineeringManager.Tests\\EngineeringManager.Tests.csproj --configuration Release
```

Expected: 首页和健康端点测试全部通过。

## Task 5: 建立 PWA 外壳和有限离线基础

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/manifest.webmanifest`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] **Step 1: 创建 PWA Manifest**

`manifest.webmanifest` 使用简体中文应用名、独立显示模式、主题色和 `/` 起始地址；图标先使用项目内静态 SVG/PNG 资源，不引用外部地址。

- [ ] **Step 2: 创建最小 Service Worker**

Service Worker 只缓存应用外壳和静态 CSS/JS，不缓存财务、工资、合同或其他业务 API 数据：

```javascript
const CACHE_NAME = 'engineering-manager-shell-v1';
const SHELL = ['/', '/css/base.css', '/css/components.css', '/css/pages.css', '/css/themes.css', '/js/site.js', '/manifest.webmanifest'];
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL)));
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key)))));
});
self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  event.respondWith(fetch(event.request).catch(() => caches.match(event.request)));
});
```

- [ ] **Step 3: 注册 Service Worker 并显示状态**

`site.js` 注册 `/service-worker.js`，只在 HTTPS 或 localhost 下启用；页面显示“已安装/可安装/离线外壳可用”状态，不显示虚假的业务同步成功。

- [ ] **Step 4: 测试外壳引用**

扩展 `HomePageTests`，断言 Manifest link、Service Worker 注册脚本和禁止外部 CDN 的约束存在。

## Task 6: 建立附件存储、日志和基础目录

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Files/IFileStore.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Files/LocalFileStore.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/App_Data/.gitkeep`
- Create: `EngineeringManager/src/EngineeringManager.Web/App_Data/attachments/.gitkeep`
- Create: `EngineeringManager/src/EngineeringManager.Web/App_Data/backups/.gitkeep`
- Create: `EngineeringManager/src/EngineeringManager.Web/App_Data/logs/.gitkeep`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/LocalFileStoreTests.cs`

- [ ] **Step 1: 定义文件存储接口**

`IFileStore` 只定义阶段 0 需要的操作：

```csharp
public interface IFileStore
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken);
    Task DeleteAsync(string storedName, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: 实现本地安全存储**

`LocalFileStore` 使用配置的 `App_Data/attachments` 根目录，文件名统一替换为 GUID 前缀 + 安全扩展名；拒绝绝对路径、`..`、目录分隔符和根目录外路径。保存、读取和删除都通过解析后的完整路径检查。

- [ ] **Step 3: 写路径穿越测试**

`LocalFileStoreTests` 至少覆盖：正常保存/读取、重复文件名不覆盖、`..\\secret.txt` 被拒绝、绝对路径被拒绝、删除后不能读取。

- [ ] **Step 4: 注册文件存储和基础日志目录**

`Program.cs` 注册 `IFileStore`，启用 ASP.NET Core 结构化日志和统一异常处理；`App_Data/logs` 作为日志落盘目录，备份目录和附件目录加入 `.gitignore`，只保留 `.gitkeep`。阶段 0 不额外实现业务级异常日志格式。

## Task 7: 建立质量门禁、README 和阶段进度更新

**Files:**
- Create: `EngineeringManager/scripts/quality-gate.ps1`
- Modify: `EngineeringManager/README.md`
- Modify: `docs/开发进度.md`
- Test: all files under `EngineeringManager/tests/EngineeringManager.Tests`

- [ ] **Step 1: 编写质量门禁脚本**

`quality-gate.ps1` 必须包含 `$ErrorActionPreference = 'Stop'`，依次执行：

```powershell
pwsh -File .\\scripts\\dotnet.ps1 restore .\\EngineeringManager.sln
pwsh -File .\\scripts\\dotnet.ps1 build .\\EngineeringManager.sln --configuration Release --no-restore
pwsh -File .\\scripts\\dotnet.ps1 test .\\EngineeringManager.sln --configuration Release --no-build
```

任一步骤非 0 退出码都必须让脚本失败。

- [ ] **Step 2: 写 README**

README 必须说明：本地 SDK 使用方式、数据库连接配置、迁移命令、运行命令、测试命令、健康端点、附件目录、备份目录、PWA 仅缓存外壳和阶段 0 尚未包含的业务模块。

- [ ] **Step 3: 执行阶段 0 完整门禁**

Run from `D:\\AI\\TZM-project\\EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -File .\\scripts\\quality-gate.ps1
```

Expected: restore、Release build、全部测试均成功，输出中没有失败测试。

- [ ] **Step 4: 更新唯一进度文档**

在 `D:\\AI\\TZM-project\\docs\\开发进度.md` 更新：

- 当前阶段改为“阶段 0：项目基线已完成”。
- 阶段路线图将阶段 0 标记为“已完成”。
- 列出所有新增/修改文件和第一条 EF Migration 名称。
- 写入质量门禁命令、构建结果、测试数量、健康检查结果和附件路径测试结果。
- 记录仍未完成的阶段 1 任务、风险和进入阶段 1 的前置条件。

## Task 8: 阶段 0 验收和提交边界

**Files:**
- Verify: `EngineeringManager/EngineeringManager.sln`
- Verify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/manifest.webmanifest`
- Verify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- Verify: `EngineeringManager/tests/EngineeringManager.Tests/`
- Verify: `docs/开发进度.md`

- [ ] **Step 1: 运行验收清单**

按以下顺序验证：

1. `pwsh -File .\\scripts\\dotnet.ps1 --version` 输出 .NET 10。
2. `pwsh -File .\\scripts\\quality-gate.ps1` 通过。
3. `GET /health/live` 返回 200。
4. SQL Server 可创建/更新 Identity 数据库。
5. 首页响应式外壳正常，页面不请求外部 CDN。
6. Manifest 和 Service Worker 可访问。
7. 文件存储路径穿越测试通过。
8. README、设计文档和唯一进度文档中的阶段 0 状态一致。

- [ ] **Step 2: 仅在用户授权后创建 Git 提交**

Git 初始化、提交和远程配置属于明确的仓库操作，只有用户确认新目录和远程地址后执行。提交边界建议：

```powershell
$ErrorActionPreference = 'Stop'
git add .
git commit -m "chore: initialize engineering manager phase 0 baseline"
```

如果用户尚未提供远程地址，不执行 `git remote add` 或 `git push`。

## 阶段 0 完成定义

阶段 0 只有在以下条件全部满足后才标记完成：

- 新项目可在本地启动。
- SQL Server Identity 数据库迁移成功。
- Release 构建成功，所有阶段 0 自动化测试通过。
- 首页、健康检查、PWA 外壳、附件基础存储可验证。
- 质量门禁脚本可重复执行。
- README 已写清本地运行方式。
- `docs/开发进度.md` 已记录完整结果和阶段 1 计划。
- 未实现的项目、合同、财务、员工业务页面没有伪装成已完成。

## 计划自检

- 设计规格的阶段 0 要求均有对应任务：基线、分层、Identity/SQL Server、响应式 UI、PWA 外壳、附件/日志、质量门禁和进度文档。
- 计划步骤均为完整执行内容，没有未完成标记。
- 项目名称、目录、程序集名称和测试项目名称在全部任务中保持一致。
- 不执行阶段 1 及之后的业务模块，避免阶段 0 范围膨胀。



