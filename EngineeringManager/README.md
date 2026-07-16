# EngineeringManager

工程项目经营管理系统的新项目目录，采用 ASP.NET Core 模块化单体架构。当前已完成阶段 0～7，后续自有公司、设备和集成交付会按 `docs/开发进度.md` 的阶段路线图连续实现。

## 本地工具

项目自带便携 PowerShell 7 和 .NET SDK，不要求修改系统级安装：

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 --version
```

当前基线为 .NET SDK `10.0.100`。EF CLI 安装在 `.tools\dotnet-tools\dotnet-ef.exe`，调用迁移时先在当前 PowerShell 会话设置本地 SDK：

```powershell
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = (Join-Path (Get-Location) '.dotnet')
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& .\.tools\dotnet-tools\dotnet-ef.exe --version
```

## 数据库

开发环境默认使用本机 SQL Server Express：`localhost\SQLEXPRESS`，数据库名为 `EngineeringManager`。连接字符串位于 `src/EngineeringManager.Web/appsettings.Development.json`；生产环境应通过 IIS 配置或环境变量提供，不把密码提交到仓库。

迁移命令：

```powershell
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = (Join-Path (Get-Location) '.dotnet')
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add <MigrationName> --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
& .\.tools\dotnet-tools\dotnet-ef.exe database update --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj
```

阶段 0 的 `InitialIdentity`、阶段 1 的 `IdentityOrganizationAuthorization`、阶段 2 的 `ProjectsContractsBillOfQuantities`、阶段 3 的 `PartnersStageResultsAttachments`、阶段 4 的 `InternalFinanceLedger`、阶段 5 的 `EmployeesPayrollLedger`、阶段 6 的 `DataExchangeBackupsReminders` 和阶段 7 的 `PwaOfflineDashboard` 迁移已在本机 SQL Server 应用成功。阶段 8 起的自动开发使用明确标识的 `EngineeringManager_Test` 测试数据库，不操作生产数据库。

阶段 1 的角色模板可以通过一次性环境变量启用幂等种子；系统不会自动生成带密码的管理员账号：

```powershell
$ErrorActionPreference = 'Stop'
$env:Identity__SeedRoles = 'true'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 run --project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj
```

## 运行、测试与质量门禁

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 run --project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln --configuration Release
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

健康端点：

- `/health/live`：进程存活检查，不访问数据库。
- `/health/ready`：应用就绪检查，包含 SQL Server 连接检查。

## 附件、备份与 PWA

- 附件默认保存到 `src/EngineeringManager.Web/App_Data/attachments`，文件名使用 GUID，拒绝路径穿越。
- 备份产物默认保存到 `src/EngineeringManager.Web/App_Data/backups`；手动备份会生成 SQL Server 数据库备份、附件 ZIP 和可追踪任务记录。
- 日志预留目录为 `src/EngineeringManager.Web/App_Data/logs`；ASP.NET Core 结构化日志仍由宿主配置决定。
- PWA Service Worker 只缓存页面外壳、CSS、JS 和 Manifest，不缓存财务、工资、合同或其他业务 API 数据。
- 阶段成果、工程量草稿、备注和照片已支持有限离线、幂等同步、版本冲突、失败重试和本机清理。
- 离线照片保存前压缩到最长边约 1920 像素、目标不超过 3 MB，每份草稿最多 20 张。

## 当前范围边界

阶段 7 已实现真实经营驾驶舱、项目阶段/金额轻量图形、匿名数据保护、阶段成果有限离线和安全 Service Worker。财务、工资、合同、结算、导入导出、备份和提醒等敏感业务响应不进入离线缓存。当前不包含复杂通用映射平台、外部云备份、外部消息通知或生产部署；自有公司、设备和最终集成交付进入阶段 8～10。
