# 阶段 2：项目、合同与工程清单实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立以项目为最高管理单位的项目主档、合同、多我方签约公司分摊、工程清单量价和项目金额汇总基础。

**Architecture:** Domain 保存项目状态、合同分摊和量价计算规则；Application 提供项目服务、合同校验和汇总查询接口；Infrastructure 使用 EF Core 持久化项目、合同、清单、分摊、节点和项目分配；Web 提供项目列表、项目详情和基础录入页面。合作单位、阶段成果和财务明细留给后续阶段，通过文本字段和可选外键预留升级空间。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：领域状态和金额计算

**Files:**
- Create: `src/EngineeringManager.Domain/Projects/ProjectStage.cs`
- Create: `src/EngineeringManager.Domain/Projects/ArchiveStatus.cs`
- Create: `src/EngineeringManager.Domain/Projects/ContractType.cs`
- Create: `src/EngineeringManager.Domain/Projects/ContractAllocationMode.cs`
- Create: `src/EngineeringManager.Domain/Projects/ProjectAmountCalculator.cs`
- Create: `src/EngineeringManager.Domain/Projects/ContractAllocationValidator.cs`
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `tests/EngineeringManager.Tests/Domain/ProjectAmountCalculatorTests.cs`
- Create: `tests/EngineeringManager.Tests/Domain/ContractAllocationValidatorTests.cs`

- [ ] 写测试，确认暂估金额=暂估量×暂估单价、结算金额=结算量×结算单价、部分结算当前金额=已结算项结算金额+未结算项暂估金额。
- [ ] 写测试，确认固定金额分摊合计必须等于合同额，比例分摊合计必须等于 100%，不合法输入抛出明确异常。
- [ ] 运行测试确认领域类型尚不存在而失败。
- [ ] 实现纯领域计算器、状态枚举、分摊模式和项目/合同管理权限键。
- [ ] 运行领域测试确认全部计算和校验通过。

## Task 2：持久化项目、合同、清单和节点

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Project.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ProjectAssignment.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ProjectLegalEntity.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ProjectMilestone.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/Contract.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ContractLegalEntityAllocation.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ContractLineItem.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ContractLineItemLegalEntityAllocation.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/ProjectModelTests.cs`

- [ ] 写 SQLite 测试，确认项目唯一编号、合同与清单项层级、两组量价、项目分配和多公司关系可以持久化。
- [ ] 运行测试确认模型尚不存在而失败。
- [ ] 添加实体、导航、索引、并发时间戳和软归档字段；项目金额汇总不持久化为可手工修改字段。
- [ ] 运行 SQLite 测试确认模型创建和关联保存通过。

## Task 3：项目与合同应用服务

**Files:**
- Create: `src/EngineeringManager.Application/Projects/ProjectDtos.cs`
- Create: `src/EngineeringManager.Application/Projects/IProjectService.cs`
- Create: `src/EngineeringManager.Application/Projects/ProjectSummaryDto.cs`
- Create: `src/EngineeringManager.Infrastructure/Projects/ProjectService.cs`
- Create: `src/EngineeringManager.Infrastructure/Projects/ProjectSummaryService.cs`
- Create: `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`

- [ ] 写服务测试，确认项目编号重复时拒绝保存，清单项总额只能通过量价计算，合同分摊校验失败时事务不保存。
- [ ] 运行服务测试确认服务尚不存在而失败。
- [ ] 实现项目新增、合同新增、清单项新增、项目分配和项目汇总查询；写入使用事务。
- [ ] 实现项目当前金额、暂估金额、结算金额和结算状态的汇总查询。
- [ ] 运行服务测试确认创建、校验和汇总通过。

## Task 4：项目页面和授权边界

**Files:**
- Modify: `src/EngineeringManager.Web/Program.cs`
- Create: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] 写 Web 测试，确认系统级管理员、应用级管理员和项目负责人可进入项目列表，查询人员可只读查看，现场人员不能进入合同编辑入口。
- [ ] 运行测试确认页面尚不存在而失败。
- [ ] 实现项目列表指标、筛选、项目详情和清单量价展示；当前金额标注“暂估/部分结算/结算”。
- [ ] 页面只调用 Application 服务，不直接拼金额或实现分摊校验。
- [ ] 运行 Web 测试确认中文页面和权限边界通过。

## Task 5：迁移、质量门禁和进度更新

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_ProjectsContractsBillOfQuantities.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 运行完整质量门禁，确认 Release 构建 0 警告、全部测试通过。
- [ ] 创建并应用阶段 2 EF Migration 到本机 SQL Server。
- [ ] 真实启动 Web，验证首页、项目列表、健康检查和数据库就绪状态。
- [ ] 更新唯一进度文件，记录迁移名称、测试数量、金额口径、遗留风险和阶段 3 计划。

## 阶段 2 完成定义

- 项目可以关联部门、分支机构、负责人、我方签约公司和项目分配。
- 合同支持主合同、补充协议、变更单和多我方公司按固定金额、比例或清单项分摊。
- 工程清单项同时保存暂估量价与结算量价，项目总额只能由清单项计算。
- 暂估、部分结算和已结算状态及当前金额有领域测试和服务测试。
- 项目页面已接入权限边界，但不提前实现合作单位、阶段成果和财务收付款。
