# Central Ledger Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 将外部账本、内部账本和中央账本录入/详情交互统一为员工管理与工资台账工作台，同时扩展安全的记录列表、详情和直接记录编辑能力。

**Architecture:** 保留现有中央账本领域实体、计算器、权限和命令服务作为事实层；在查询层增加按记录类型的安全投影 DTO，在 Razor 页面使用真实 `view` 标签驱动当前业务区；详情通过 GET handler + fetch 弹窗加载，录入页面使用统一 dialog 和动态字段脚本。页面只消费 DTO，不直接序列化 EF 实体。

**Tech Stack:** ASP.NET Core Razor Pages, C# records, EF Core, vanilla ES modules, existing CSS variables/components, xUnit + FluentAssertions.

---

### Task 1: 固化任务上下文与页面契约

**Files:**
- Modify: `.trellis/tasks/07-31-central-ledger-workspace-redesign/prd.md`
- Create: `.trellis/tasks/07-31-central-ledger-workspace-redesign/design.md`
- Create: `EngineeringManager/docs/superpowers/specs/2026-07-31-central-ledger-workspace-design.md`
- Create: `EngineeringManager/docs/superpowers/plans/2026-07-31-central-ledger-workspace.md`

- [x] 记录已确认的左右工作台、真实标签、统一弹窗、分摊和锁定规则。
- [x] 明确 DTO、handler、样式、权限、错误处理和验证边界。

### Task 2: 扩展中央账本安全查询投影

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/CentralLedgerQueryService.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`

- [ ] 在 `CentralLedgerOverviewPageDto` 末尾增加可选的发票、资金、扣款、待分摊发票和审计集合，保持已有构造调用兼容。
- [ ] 为每类列表定义不含 EF 导航循环的安全 DTO，并在 `SearchAsync` 中按现有范围/日期/方向/主体筛选投影。
- [ ] 将结算来源类型/来源 ID/来源 URL暴露给操作列和详情来源区。
- [ ] 扩展 `GetAsync` 的安全 header JSON，包含显示名称、来源、状态和可读金额字段。
- [ ] 为外部/内部、待分摊和来源权限补充查询测试。

### Task 3: 将外部账本改造成左右真实标签工作台

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/central-ledger-workspace.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] 增加 `View`、详情 handler 和编辑入口上下文，标签链接保留全部公共筛选参数。
- [ ] 左侧显示六项可点击汇总卡和快捷分类，右侧只渲染当前标签的业务区。
- [ ] 为结算、资金、发票、扣款、待分摊、异常和工资付款渲染真实列表及统一操作列。
- [ ] 对年度账、对账、修改记录渲染摘要卡和跳转按钮，避免复制下层实体。
- [ ] 使用统一详情 dialog，JS fetch 当前页面 handler 并渲染分区、金额语义色、分摊和来源。

### Task 4: 将内部账本对齐同一工作台壳

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/central-ledger-workspace.js`

- [ ] 复用外部账本工作台脚本和详情结构，使用内部专属标签与本方/对方自有公司字段。
- [ ] 过滤器隐藏外部合作单位，所有新增链接携带 `scope=Internal` 和当前 `view`。
- [ ] 内部转账、内部发票、待分摊和异常使用对应 DTO 或空状态。

### Task 5: 将统一录入页改为动态大弹窗并移除裸删除表单

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/central-ledger-entry.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] 使用自动打开的大 dialog，顶部显示账本范围、来源说明和关闭按钮。
- [ ] 使用记录类型标签切换字段；外部/内部主体、方向、账户、发票和分摊字段动态显示。
- [ ] 显示金额预览：原始金额、应开票、已分摊、本次分摊、剩余待分摊。
- [ ] 编辑 URL 带入 `type`、`id`、`stamp` 时加载直接中央记录；来源/已分摊记录显示只读提示。
- [ ] 删除改为从详情弹窗打开二次确认 dialog，原因必填，保存当前列表上下文。

### Task 6: 增加直接记录更新契约与锁定验证

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/ICentralLedgerCommandService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/CentralLedgerCommandService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Entries/Edit.cshtml.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerCommandServiceTests.cs`

- [ ] 增加结算、发票、资金直接记录更新请求，携带并发版本和修改原因。
- [ ] 服务层只允许 `CentralLedger` 来源的未分摊直接记录完整更新；已分摊核心字段拒绝并提示调整路径。
- [ ] 中央账本入口不允许删除来源模块生成的记录；保留其他入口兼容旧审计测试。
- [ ] 新增并发过期、来源只读、已分摊锁定和更新审计测试。

### Task 7: 页面测试、构建与回归验证

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/CentralLedgerWorkspaceInteractionTests.cs`

- [ ] 增加源码测试覆盖真实标签、左右工作台、详情 handler、动态录入、来源只读和删除确认。
- [ ] 运行中央账本应用/领域/页面测试。
- [ ] 运行完整 `dotnet test` 和 `dotnet build`，修复编译、Razor 和 JS 资源引用问题。
- [ ] 检查 `git diff --check` 与页面关键字符串，确认没有遗漏旧裸删除入口或伪标签链接。
