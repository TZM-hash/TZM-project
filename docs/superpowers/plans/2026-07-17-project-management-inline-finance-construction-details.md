# Project Management Inline Finance and Construction Details Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成项目总览字段调整、五个标签原位编辑、正式财务同步和设备/施工班组施工详情管理。

**Architecture:** 项目基础日期继续归属 `Project`；施工详情使用独立 `ProjectConstructionRecord` 作为项目施工事实表；设备与班组主档复用现有正式服务。财务更新扩展现有 `FinanceLedgerService`，在事务中同步正式记录、账户流水、发票分配和审计日志。

**Tech Stack:** ASP.NET Core Razor Pages、EF Core、SQL Server、xUnit、FluentAssertions、原生 JavaScript/CSS、Playwright 桌面验收。

---

### Task 1: 锁定项目总览和原位编辑验收标准

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`

- [ ] 增加失败测试：总览不包含“上级项目”“分支机构”，包含实际开工、实际完工和三个比例。
- [ ] 增加失败测试：五个标签均存在 `data-inline-cell-edit` 或逐行原位控件，财务标签不得使用独立 `inline-edit-grid` 新增表单代替现有行。
- [ ] 增加失败测试：项目经理可看到财务快捷编辑，查询用户看不到任何编辑控件。
- [ ] 使用项目本地 PowerShell 7 和 `.dotnet/dotnet.exe` 运行定向测试，确认因功能缺失失败。

### Task 2: 增加项目实际日期

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Project.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Edit.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Edit.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`

- [ ] 写失败测试：保存和读取实际开工/完工日期，拒绝完工早于开工。
- [ ] 在 DTO、请求和服务中贯通两个日期，审计快照包含日期。
- [ ] 从项目详情总览删除上级项目和分支机构显示/快捷编辑，保留底层旧字段不删除。
- [ ] 在详细编辑页和原位编辑中增加实际开工、实际完工日期。
- [ ] 运行项目服务和页面定向测试至通过。

### Task 3: 统一三个比例数据源与展示

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] 写失败测试：比例标签和 `mini-progress` 存在，并使用详细页 `FinanceSummary` 数值。
- [ ] 实现收款、开票、付款比例计算；分母为 0 时为 0%，进度条宽度限制 0-100%。
- [ ] 将比例放入项目总览资料网格，保持只读。
- [ ] 运行页面定向测试至通过。

### Task 4: 为正式财务记录增加更新服务

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/FinanceDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`

- [ ] 分别为应收、收款、发票、应付、付款编写更新失败测试，覆盖并发版本和修改原因。
- [ ] 编写失败测试：收款/付款变更账户、日期、金额后对应 `AccountTransaction` 同步更新。
- [ ] 编写失败测试：发票金额变化后关联分配按比例重算，合计不超过新含税金额。
- [ ] 实现五类更新请求和事务更新方法，记录完整前后快照及项目关联审计。
- [ ] 扩展项目工作台 DTO，使每条财务明细包含编辑所需 ID、外键和并发版本。
- [ ] 运行财务与工作台服务定向测试至通过。

### Task 5: 将工程量和财务明细改为逐行原位编辑

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/quick-edit.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`

- [ ] 为五个标签写失败页面测试：原值和同格控件并存，控件预填正式值，每行有保存与取消。
- [ ] 把财务三页的独立新增表单替换为现有记录逐行编辑；新增业务使用单独“新增记录”行。
- [ ] 项目经理获得项目页财务编辑权限；财务角色仅显示财务编辑。
- [ ] 保存失败时根据记录 ID 恢复当前行编辑状态并保留 ModelState 输入。
- [ ] 增加 JavaScript 单行隔离、取消复原和预填测试标记，运行页面测试至通过。

### Task 6: 新增施工详情领域模型与迁移前测试

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Domain/Projects/ProjectConstructionRecordType.cs`
- Create: `EngineeringManager/src/EngineeringManager.Domain/Projects/ProjectConstructionCalculator.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ProjectConstructionRecord.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Project.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Domain/ProjectConstructionCalculatorTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/ProjectConstructionModelTests.cs`

- [ ] 写失败计算测试：首尾计入、未退场算至指定今天、停工扣减、非法日期和停工天数拒绝。
- [ ] 写失败模型测试：设备/班组二选一、多次进退场、自引用上一条/下一条及项目索引。
- [ ] 实现枚举、纯计算器、实体和 EF 配置。
- [ ] 运行领域与模型测试至通过。

### Task 7: 实现施工记录服务、自动流转和正式主档同步

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Projects/IProjectConstructionService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectConstructionDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectConstructionService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectConstructionServiceTests.cs`

- [ ] 写失败测试：新增、修改、并发、设备/班组校验、多次进退场和审计。
- [ ] 写失败测试：选择调出项目自动创建目标项目待完善记录并双向连接。
- [ ] 写失败测试：重连解除旧关系，禁止自连、循环和重复待完善记录。
- [ ] 写失败测试：项目内新建设备/班组调用正式主档服务并立即用于施工记录。
- [ ] 实现查询、保存、流转、自动匹配和主档创建服务，注册依赖注入。
- [ ] 运行施工服务定向测试至通过。

### Task 8: 构建施工详情标签与原位编辑

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/quick-edit.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] 写失败页面测试：付款右侧存在施工详情标签和规定的十一列。
- [ ] 增加施工记录加载、设备/班组/项目选项、保存处理器和权限控制。
- [ ] 实现新增设备行、新增班组行、正式主档内联创建、逐行快捷编辑、自动连接上/下项目按钮。
- [ ] 总天数和施工天数在页面加载及日期/停工输入变化时实时计算。
- [ ] 运行项目页面定向测试至通过。

### Task 9: 生成并应用数据库迁移

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_ProjectDatesAndConstructionDetails.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] 停止 5075 本地服务，使用项目本地 PowerShell 7、.NET 10 和本地 `dotnet-ef` 生成迁移。
- [ ] 检查迁移只新增项目日期、施工记录表、外键和索引，不删除现有数据。
- [ ] 仅向 `EngineeringManager_Test` 应用迁移，并运行 `dotnet ef migrations has-pending-model-changes`。
- [ ] 运行迁移及样例数据库相关测试。

### Task 10: 完整验证、文档和运行恢复

**Files:**
- Modify: `docs/开发进度.md`
- Modify when required: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`

- [ ] 运行全部定向测试和完整 `dotnet test EngineeringManager.sln -c Release --no-restore`。
- [ ] 如项目快捷编辑脚本发生变化，升级 Service Worker shell 版本，避免旧缓存导致按钮无效。
- [ ] 启动 Release 服务并确认 `/health/ready` 为 200。
- [ ] 使用正式演示管理员在 1440px 浏览器验证总览、五个标签、原位预填、财务同步、施工多次进退场、主档新建和项目自动流转。
- [ ] 确认页面宽度等于视口、无控制台/脚本/HTTP 错误；不做手机端验证。
- [ ] 更新 `docs/开发进度.md`，记录迁移、测试数量、桌面验收、服务地址和未执行 Git 操作。

