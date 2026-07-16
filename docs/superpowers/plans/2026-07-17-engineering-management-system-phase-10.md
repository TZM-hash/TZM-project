# 阶段 10：集成验收与交付实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用可重复的样例数据、全模块回归、安全/性能/备份恢复验证和部署产物，证明阶段 0～9 形成一个可交付但尚未实际部署生产的系统。

**Architecture:** Development 专用幂等种子向 `EngineeringManager_Test` 写入代表性数据，所有验收脚本显式接收测试连接字符串并拒绝生产标识。阶段 10 不新增业务平台，只补足跨模块测试、性能基线、恢复演练、部署包和运维文档。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、PowerShell 7、现有 xUnit/FluentAssertions、现有 SimpleXlsx、Windows Server/IIS 发布工具链。

---

## Task 1：Development 测试管理员与幂等样例数据

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/DevelopmentSampleDataSeeder.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Development/IDevelopmentSampleDataSeeder.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/appsettings.Development.json`
- Modify: `EngineeringManager/.gitignore`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`

- [ ] 写失败测试：非 Development 拒绝、数据库名不是 `EngineeringManager_Test` 拒绝、重复执行不重复、清理后重建、账号/公司/项目/财务/员工/设备场景齐全。
- [ ] 实现显式开关 `DevelopmentSampleData:Enabled` 和数据库名安全护栏。

```csharp
if (!environment.IsDevelopment() || !db.Database.GetDbConnection().Database.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("样例数据只能写入明确标识的测试数据库。");
}
```

- [ ] 自动生成满足 Identity 规则但易输入的本地管理员密码，把账号和密码写入 `App_Data/local-test-credentials.txt`；确认该路径被 Git 忽略。
- [ ] 创建代表性小样例和性能样例两种规模，所有业务名称含“测试”。
- [ ] 重跑种子测试确认 GREEN。

## Task 2：跨模块业务回归

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Integration/EndToEndBusinessFlowTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Integration/CompanyEquipmentFinanceIntegrationTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Integration/ImportExportRoundTripTests.cs`

- [ ] 写并运行项目全流程：公司→项目→合同→暂估量价→阶段成果→结算→应收→收款→销项发票→导出。
- [ ] 写并运行员工全流程：员工→工资批次→项目/公司成本→多次支付→报销/借支/其他往来。
- [ ] 写并运行设备全流程：租赁约定→进场→日期段→退场→终结算→生成应付→付款/进项发票；自有设备只形成项目内部成本。
- [ ] 写 Excel 导出再导入预览的关键字段往返测试，确认公司和设备自由字段模板稳定。
- [ ] 所有测试使用独立测试数据库事务或 SQLite，不依赖执行顺序。

## Task 3：权限、安全、注册和匿名泄露审计

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Security/FinalAuthorizationMatrixTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Security/AnonymousDataLeakTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Security/AttachmentAccessTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`

- [ ] 生成系统管理员、应用管理员、财务、项目负责人、现场人员、设备管理员和查询人员矩阵测试，覆盖菜单、读取、写入、公司/项目数据范围。
- [ ] 验证公开注册关闭、匿名首页不泄露金额、所有业务页跳转登录、附件下载再次授权、逻辑删除和审计快照。
- [ ] 静态验证 Service Worker 不缓存财务、工资、合同、结算、公司敏感详情、设备租金和 API 响应。
- [ ] 运行全部 Security/Web 测试确认 GREEN。

## Task 4：代表性数据性能基线

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Performance/RepresentativeDataPerformanceTests.cs`
- Create: `EngineeringManager/scripts/performance-baseline.ps1`
- Create: `EngineeringManager/docs/performance-baseline.md`

- [ ] 使用约 100 项目、500 员工、200 设备和 1 万财务/使用记录生成性能数据。
- [ ] 测量公司/项目/设备常用列表与详情、首页汇总和常用 Excel 导出；预热后记录中位数与最大值。
- [ ] 目标：常用列表/详情 2 秒内、首页汇总 3 秒内、Excel 导出 15 秒内；超标先定位查询、索引或 N+1 根因再优化。
- [ ] 脚本输出机器、数据量、命令、耗时和“非生产并发容量”声明到性能文档。

## Task 5：数据库与附件备份恢复演练

**Files:**
- Create: `EngineeringManager/scripts/verify-backup-restore.ps1`
- Create: `EngineeringManager/docs/backup-restore-verification.md`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/BackupServiceTests.cs`

- [ ] 脚本首行设置 `$ErrorActionPreference = 'Stop'`，只接受源库 `EngineeringManager_Test` 和目标库 `EngineeringManager_RestoreVerification`。
- [ ] 调用现有备份服务生成数据库备份和附件 ZIP，恢复到临时库并核对迁移历史、核心表计数、关键汇总和附件清单。
- [ ] 验证完成后再次确认目标数据库名并删除临时恢复库；绝不覆盖或删除 `EngineeringManager_Test`。
- [ ] 把命令、备份文件、计数和结果写入恢复验证文档。

## Task 6：Release 发布包与部署手册

**Files:**
- Create: `EngineeringManager/scripts/publish-release.ps1`
- Create: `EngineeringManager/docs/deployment-windows-iis.md`
- Create: `EngineeringManager/docs/release-checklist.md`
- Modify: `EngineeringManager/README.md`

- [ ] 编写 PowerShell 发布脚本，使用项目内 SDK执行 `dotnet publish --configuration Release`，输出到 Git 忽略的 `artifacts/publish`。
- [ ] 部署手册包含 Windows Server/IIS Hosting Bundle、应用池、目录权限、HTTPS、生产连接字符串环境变量、迁移、附件、备份、日志和回滚。
- [ ] 发布脚本不写生产密码、不连接生产服务器、不自动执行生产迁移。
- [ ] 发布清单明确测试库凭据不得复制到生产；`EngineeringManager_Test` 在实际生产确认前保留，之后再人工删除。
- [ ] 生成发布包并检查 Web DLL、静态资源、Manifest、配置模板和说明文件齐全。

## Task 7：最终质量门禁、HTTP 与浏览器验收

**Files:**
- Modify: `EngineeringManager/scripts/quality-gate.ps1`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/HealthEndpointTests.cs`

- [ ] 完整执行 restore、格式/分析器、Release build、全部测试和 publish，要求 0 警告、0 错误、0 失败。
- [ ] 对 `EngineeringManager_Test` 运行全部迁移并确认无待应用迁移。
- [ ] 启动 Release Web，验证 live/ready、登录、管理员、公司、项目、财务、员工、设备、导入导出、提醒、备份和离线端点。
- [ ] 浏览器检查桌面和 390px 手机关键页面、控制台、离线草稿刷新保留、冲突/重试、匿名泄露和登录/退出。
- [ ] 记录无法在无真实生产环境验证的 IIS/TLS/域名事项，不把它们伪装为通过。

## Task 8：最终文档、完成审计和本地提交

**Files:**
- Modify: `docs/superpowers/specs/2026-07-16-engineering-management-system-design.md`
- Modify: `docs/superpowers/specs/2026-07-16-engineering-management-system-design.docx`
- Modify: `docs/开发进度.md`
- Modify: `EngineeringManager/README.md`

- [ ] 对照总体设计第 21、27、28、29 节和阶段 7～10计划逐项建立完成证据表，任何缺口必须实现或明确列为外部样例/生产部署遗留。
- [ ] 更新唯一进度文件：阶段 0～10状态、迁移、测试总数、性能结果、恢复结果、发布包路径、已知风险和生产部署下一步。
- [ ] 重新生成 Word 设计文档并检查标题、关键章节、页数和可打开性。
- [ ] 运行 `git diff --check`、确认没有密码/密钥进入 Git、确认工作区只有预期产物。
- [ ] 本地提交阶段 10，不推送远端；阶段 10完成后暂停等待实际生产部署安排。

## 阶段 10 完成定义

- 代表性样例、跨模块回归、权限安全、性能、备份恢复和发布包都有当前证据。
- `EngineeringManager_Test` 是唯一持久化测试库，阶段 10后保留到生产部署确认；临时恢复库已删除。
- 完整质量门禁 0 失败、Release 0 警告/0 错误，关键 HTTP/浏览器流程通过。
- 总体设计、Word 版、README 和唯一进度文档一致，所有阶段本地提交且未推送远端。
