# EngineeringManager

工程项目经营管理系统的新项目目录，采用 ASP.NET Core 模块化单体架构。阶段 0～10 已完成，本地测试库、发布包、性能基线、备份恢复演练和部署手册均已形成；尚未实际部署生产环境。

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

开发与自动验收默认使用本机 SQL Server Express：`localhost\SQLEXPRESS`，数据库名为 `EngineeringManager_Test`。测试库连接字符串位于 `src/EngineeringManager.Web/appsettings.Development.json`；生产环境必须通过 IIS 配置或环境变量提供正式连接字符串，不把密码提交到仓库，也不得复用测试库。

迁移命令：

```powershell
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = (Join-Path (Get-Location) '.dotnet')
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add <MigrationName> --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
& .\.tools\dotnet-tools\dotnet-ef.exe database update --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj
```

阶段 0～8 的历史迁移、阶段 9 的 `EquipmentManagement` 和 `EquipmentOfflinePhotos` 已应用到 `EngineeringManager_Test`。该测试库会保留到未来实际生产部署并确认正式系统可用后再删除；自动开发不操作生产数据库。

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

Development 测试管理员与样例数据默认关闭，必须显式开启，且服务会再次校验数据库名以 `_Test` 结尾：

```powershell
$ErrorActionPreference = 'Stop'
$env:Identity__SeedRoles = 'true'
$env:DevelopmentSampleData__Enabled = 'true'
& .\scripts\dotnet.ps1 run --project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --configuration Release
```

账号密码写入 Git 忽略的 `src/EngineeringManager.Web/App_Data/local-test-credentials.txt`，不得复制到生产环境。

如需按确认方案删除并重建完整测试库，使用带三重安全校验的脚本。脚本只接受 Development 配置中的 `EngineeringManager_Test`，会应用全部 Migration、生成五类测试账号和完整关联样例；任何数据库名不一致或不以 `_Test` 结尾的请求都会被拒绝：

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\reset-test-database.ps1 -DatabaseName 'EngineeringManager_Test'
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
- 设备进退场、施工/停工日期段、备注和照片使用同一 IndexedDB 按用户隔离，支持幂等同步、版本冲突、照片独立重试和本机清理；租金、结算与财务端点不进入离线缓存。
- 离线照片保存前压缩到最长边约 1920 像素、目标不超过 3 MB，每份草稿最多 20 张。

## 当前范围边界

阶段 8 已扩展现有 `LegalEntity` 为轻量自有公司管理。阶段 9 已交付完整设备管理与现场有限离线。阶段 10 已交付幂等测试样例、最终权限/业务回归、代表性性能基线、SQL Server + 附件备份恢复演练、Release 发布包和 Windows Server + IIS 部署手册。公开注册已关闭；系统不建设公司内部往来台账。生产服务器、域名、TLS 和正式数据导入须在实际部署窗口按发布清单执行。
