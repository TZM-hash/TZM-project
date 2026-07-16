# EngineeringManager

工程项目经营管理系统的新项目目录，采用 ASP.NET Core 模块化单体架构。当前已完成阶段 0～9，正在按 `docs/开发进度.md` 进入阶段 10 集成验收与交付。

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

阶段 8 已扩展现有 `LegalEntity` 为轻量自有公司管理。阶段 9 已交付逐台设备档案、自有/租赁权属、项目进退场、多段施工/停工日期、租金计算、押金预付款、单次最终结算、应付关联、转让、非必填维保、两类到期提醒、Excel 导入导出以及设备现场有限离线。公开注册已关闭；系统不建设公司内部往来台账。阶段 10 负责全模块回归、性能、备份恢复、发布包和部署手册，不实际部署生产环境。
