# 阶段 7：PWA 有限离线与整体图形化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为阶段成果提供安全、幂等、可冲突处理的有限离线能力，并把首页升级为真实、响应式、无额外图表依赖的经营驾驶舱。

**Architecture:** 浏览器用 IndexedDB 保存本用户草稿、压缩照片和同步队列；服务端以离线同步映射表、现有 StageResult 并发标记和独立照片幂等键作为权威同步边界。首页由 DashboardService 聚合项目、财务、工资、提醒数据，Razor 以语义化 HTML/CSS 水平条形图渲染，Service Worker 只缓存静态外壳和离线页面，不缓存核心业务响应。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、ASP.NET Core Identity、IndexedDB、Service Worker、Canvas 图片压缩、HTML/CSS/SVG、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：离线同步领域规则

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Domain/Offline/OfflineSyncEnums.cs`
- Create: `EngineeringManager/src/EngineeringManager.Domain/Offline/OfflinePhotoPolicy.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Domain/OfflineSyncPolicyTests.cs`

- [ ] 写测试，确认离线照片最多 20 张、单张服务器接收上限 3 MB，合法边界通过，超限拒绝。
- [ ] 写测试，确认同步状态包含已同步、失败、冲突，冲突解决只允许保留服务器或基于服务器重试。
- [ ] 运行 `dotnet test --filter FullyQualifiedName~OfflineSyncPolicyTests`，确认类型不存在而 RED。
- [ ] 实现最小枚举和纯领域校验，不引入浏览器或 EF 依赖。
- [ ] 重跑领域测试，确认 GREEN。

## Task 2：离线映射模型与数据约束

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/OfflineDraftSync.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/OfflineAttachmentSync.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/OfflineSyncModelTests.cs`

- [ ] 写 SQLite 测试，确认 `(UserId, ClientDraftId)` 唯一、`(OfflineDraftSyncId, ClientAttachmentId)` 唯一。
- [ ] 写测试，确认同步记录关联 StageResult，照片映射关联 Attachment，删除行为不会级联删除正式阶段成果或附件历史。
- [ ] 运行模型测试，确认 DbSet/实体不存在而 RED。
- [ ] 实现实体、DbSet、长度、索引、并发/状态字段和 Restrict 外键。
- [ ] 重跑模型测试，确认 GREEN。

## Task 3：幂等草稿与照片同步服务

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Offline/OfflineSyncDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Offline/IOfflineStageResultService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Offline/OfflineStageResultService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/OfflineStageResultServiceTests.cs`

- [ ] 写测试，首次同步创建一个 StageResult 草稿并返回服务器 ID/版本，同一客户端 ID 重试不重复创建。
- [ ] 写测试，版本一致时更新字段和工程量行并轮换 ConcurrencyStamp。
- [ ] 写测试，服务器版本变化返回 Conflict 和服务器快照，正式记录/作废记录拒绝覆盖。
- [ ] 写测试，项目负责人和现场人员只能同步分配给自己的项目，管理员可同步全部有效项目。
- [ ] 写测试，照片首次上传创建安全附件，重复客户端照片 ID 返回原附件，超过 20 张或 3 MB 拒绝。
- [ ] 写测试，业务失败和冲突保存状态/错误，成功后清除旧错误。
- [ ] 运行服务测试，确认接口不存在而 RED。
- [ ] 实现项目选项查询、草稿 upsert、工程量重新计算、照片 IFileStore 保存和幂等事务。
- [ ] 重跑服务测试，确认 GREEN。

## Task 4：离线页面 JSON/文件端点与授权

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/StageResults/Offline.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/StageResults/Offline.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/OfflineStageResultAuthorizationTests.cs`

- [ ] 写 Web 测试，确认系统/应用管理员、项目负责人、现场人员可进入离线页，财务和查询人员返回 403，匿名返回登录跳转。
- [ ] 写端点测试，确认 JSON 同步和 multipart 照片处理器要求登录、角色和防伪令牌，服务调用使用当前用户 ID 而不是客户端传入用户。
- [ ] 运行 Web 测试，确认页面不存在而 RED。
- [ ] 实现离线页面、项目选项、草稿同步、照片同步和失败上报 handlers，并在导航增加入口。
- [ ] 重跑 Web 测试，确认 GREEN。

## Task 5：IndexedDB、照片压缩和同步队列

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/offline-stage-results.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/StageResults/Offline.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/OfflineAssetsTests.cs`

- [ ] 写静态资产测试，确认数据库含 drafts/photos/queue/metadata 四个 store，数据按用户分区。
- [ ] 写测试，确认 20 张限制、Canvas 最长边 1920、3 MB 目标、storage estimate、清理本机数据和冲突动作入口存在。
- [ ] 写测试，确认队列在 online 事件/手动操作时启动，递增退避且不把网络提示当作成功。
- [ ] 运行资产测试，确认 JS 不存在而 RED。
- [ ] 实现 IndexedDB 封装、草稿表单保存、照片压缩、队列、状态计数、冲突比较和清理操作。
- [ ] 重跑资产测试，确认 GREEN。

## Task 6：真实经营驾驶舱和轻量图表

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Dashboard/DashboardDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Dashboard/IDashboardService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] 写服务测试，确认空数据库返回全零而不是异常。
- [ ] 写服务测试，确认项目阶段分布、当前工程金额、应收/已收、应付/已付、应开票/已开票、未发工资和未处理提醒口径。
- [ ] 写 Web 测试，匿名首页不包含业务金额；登录用户首页包含指标卡、阶段条形图、金额对比、风险和离线状态挂载点。
- [ ] 运行 Dashboard/Home 测试，确认服务和新标记不存在而 RED。
- [ ] 实现聚合服务、匿名/登录双状态首页和直接标注的 CSS 水平条形图；数值始终可见，无 hover-only 交互。
- [ ] 重跑测试，确认 GREEN。

## Task 7：提醒中心与安全 Service Worker

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Reminders/ReminderService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ReminderServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] 写测试，冲突/失败同步记录生成 OfflineSyncFailed 提醒，恢复成功后提醒可解决。
- [ ] 写静态测试，Service Worker 缓存版本升级，只缓存静态外壳；`/api/` 及财务、工资、合同、结算、导入导出、备份、提醒不进入缓存。
- [ ] 运行测试，确认规则缺失而 RED。
- [ ] 实现提醒 upsert/resolve、Service Worker 路由分流、旧缓存清理和更新提示。
- [ ] 重跑相关测试，确认 GREEN。

## Task 8：迁移、浏览器验收、文档和阶段收尾

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_PwaOfflineDashboard.cs`
- Modify: `EngineeringManager/README.md`
- Modify: `docs/superpowers/specs/2026-07-16-engineering-management-system-design.md`
- Modify: `docs/开发进度.md`

- [ ] 生成 `PwaOfflineDashboard` Migration；若生成文件触发 CA1861，仅在生成文件局部禁用。
- [ ] 应用迁移到本机 SQL Server，确认 OfflineDraftSyncs/OfflineAttachmentSyncs 和索引存在。
- [ ] 运行阶段 7 定向测试、完整 `scripts/quality-gate.ps1`，要求 0 警告、0 错误、全部测试通过。
- [ ] 真实启动 Release Web，验证 live/ready=200、匿名首页=200、匿名离线页=302、授权页面和同步端点符合预期。
- [ ] 用浏览器检查桌面与 390px 手机布局、离线保存/刷新保留、断网重试、冲突提示、照片失败重试和本机清理。
- [ ] 更新总体设计：记录浏览器 A、照片 A，以及已确认但延后的自有公司/设备需求。
- [ ] 更新 README 和唯一 `docs/开发进度.md`，记录迁移、测试总数、范围边界和后续阶段路线图。
- [ ] 完成代码审查与 verification-before-completion，提交阶段 7 分支；不自动实现自有公司和设备模块。

## 阶段 7 完成定义

- 断网时可创建和重开本用户阶段成果草稿，保存工程量、备注和最多 20 张压缩照片。
- 联网后草稿与照片幂等同步；重复重试不产生重复记录；服务器版本变化明确冲突且不静默覆盖。
- 首页展示真实经营指标、项目阶段、金额对比、风险和本机同步状态，匿名用户看不到业务数据。
- Service Worker 不缓存财务、工资、合同、结算等核心业务响应。
- 桌面和手机端可用，支持指定浏览器范围，所有关键数值无需悬停即可读取。
- Migration 已应用，完整质量门禁和真实 HTTP/浏览器验收通过，唯一进度文档已更新。

