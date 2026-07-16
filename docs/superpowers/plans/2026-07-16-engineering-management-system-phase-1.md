# 阶段 1：身份、组织与权限实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在阶段 0 基线之上实现可审计的用户、组织、我方签约公司和权限基础，使系统级管理员与应用级管理员可以安全地管理后续业务模块所需的数据范围。

**Architecture:** Domain 保存组织、权限和数据范围的无框架模型；Application 保存权限评估和组织服务接口；Infrastructure 使用 EF Core/Identity 持久化组织、公司、用户关联、权限覆盖、数据范围和审计日志；Web 提供最小的管理员总览、组织管理和用户管理页面。业务模块暂不在本阶段加入。

**Tech Stack:** .NET 10、ASP.NET Core Identity、EF Core SQL Server、Razor Pages、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：建立领域模型和权限目录

**Files:**
- Create: `src/EngineeringManager.Domain/Organization/OrganizationUnit.cs`
- Create: `src/EngineeringManager.Domain/Organization/OrganizationUnitType.cs`
- Create: `src/EngineeringManager.Domain/Organization/LegalEntity.cs`
- Create: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `src/EngineeringManager.Domain/Security/SystemRoles.cs`
- Create: `src/EngineeringManager.Domain/Security/PermissionEffect.cs`
- Create: `tests/EngineeringManager.Tests/Domain/PermissionCatalogTests.cs`

- [ ] 写测试，确认系统级管理员拥有全局角色名，应用级管理员拥有基础管理权限，未知权限键默认拒绝。
- [ ] 运行 Domain 测试，确认先以缺少类型/成员的方式失败。
- [ ] 添加组织、签约公司、角色和权限常量的最小实现。
- [ ] 重新运行测试，确认权限目录测试通过。

## Task 2：持久化组织、签约公司和用户关联

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/OrganizationUnit.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/LegalEntity.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/UserOrganizationMembership.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/UserLegalEntityAccess.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationUser.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/OrganizationModelTests.cs`

- [ ] 写 SQLite 测试，确认部门/分支机构层级、签约公司唯一编码、用户主部门和签约公司访问关系可以保存。
- [ ] 运行测试确认模型尚不存在而失败。
- [ ] 添加 EF 实体、索引、唯一约束、软停用字段和导航关系。
- [ ] 运行测试确认组织模型可以创建并持久化。

## Task 3：权限覆盖、数据范围和审计日志基础

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/UserPermissionOverride.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/UserDataScope.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/AuditLog.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PermissionScopeType.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `src/EngineeringManager.Application/Security/PermissionEvaluator.cs`
- Create: `src/EngineeringManager.Application/Security/PermissionOverrideDto.cs`
- Create: `tests/EngineeringManager.Tests/Application/PermissionEvaluatorTests.cs`

- [ ] 写测试，确认系统级管理员全允许、显式拒绝优先、应用级管理员不能访问系统安全配置、普通角色未授权默认拒绝。
- [ ] 运行测试确认评估器尚不存在而失败。
- [ ] 实现纯内存权限评估器、权限覆盖实体、数据范围实体和审计日志实体。
- [ ] 配置 JSON 修改前/后值、操作人、IP、请求 ID、业务对象和修改原因字段。
- [ ] 运行测试确认权限评估器和 SQLite 模型通过。

## Task 4：组织服务和管理员页面

**Files:**
- Create: `src/EngineeringManager.Application/Organization/IOrganizationService.cs`
- Create: `src/EngineeringManager.Application/Organization/OrganizationDtos.cs`
- Create: `src/EngineeringManager.Infrastructure/Organization/OrganizationService.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`
- Create: `src/EngineeringManager.Web/Pages/Admin/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Admin/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Admin/Organizations.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Admin/Organizations.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Web/AdminAuthorizationTests.cs`

- [ ] 写 Web 测试，确认未登录访问管理员页面被拒绝，系统级管理员和应用级管理员可访问组织页，查询人员不能访问。
- [ ] 运行测试确认管理员页面/策略尚不存在而失败。
- [ ] 实现组织查询、部门/分支机构/签约公司新增和停用服务，页面使用服务层而不直接写复杂业务逻辑。
- [ ] 注册管理员授权策略和服务，添加中文管理员总览及组织列表页面。
- [ ] 运行 Web 测试确认权限边界和页面响应正确。

## Task 5：用户管理、角色模板和初始种子

**Files:**
- Create: `src/EngineeringManager.Application/Users/IUserAdministrationService.cs`
- Create: `src/EngineeringManager.Application/Users/UserAdministrationDtos.cs`
- Create: `src/EngineeringManager.Infrastructure/Users/UserAdministrationService.cs`
- Create: `src/EngineeringManager.Infrastructure/Identity/IdentitySeed.cs`
- Modify: `src/EngineeringManager.Web/Program.cs`
- Create: `src/EngineeringManager.Web/Pages/Admin/Users.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Admin/Users.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/IdentitySeedTests.cs`

- [ ] 写测试，确认角色模板幂等创建，系统级管理员角色存在，应用级管理员不能被应用管理员页面任命。
- [ ] 运行测试确认种子和用户服务尚不存在而失败。
- [ ] 实现角色模板种子、用户列表、启用/停用和角色分配服务；不在仓库保存初始密码。
- [ ] 实现管理员用户页，显示启用状态、主部门、角色和签约公司访问范围。
- [ ] 运行测试确认种子幂等和页面授权通过。

## Task 6：迁移、质量门禁和阶段进度

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_IdentityOrganizationAuthorization.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 运行完整质量门禁，确认 Release 构建 0 警告、全部测试通过。
- [ ] 创建并应用阶段 1 EF Migration 到本机 SQL Server。
- [ ] 真实启动 Web，验证管理员页面的未登录边界、健康检查和数据库就绪状态。
- [ ] 更新唯一进度文件，记录迁移名称、文件、测试数量、遗留风险和阶段 2 计划。

## 阶段 1 完成定义

- 用户、组织、签约公司、角色模板、权限覆盖和数据范围模型可以迁移并持久化。
- 系统级管理员和应用级管理员边界有自动化测试；未登录和查询人员不能进入管理页面。
- 组织和用户管理页面使用服务层，页面不直接实现复杂权限或数据规则。
- 审计日志基础表保存完整修改前后 JSON、操作人、请求标识、IP、原因和业务对象字段。
- 阶段 1 只实现权限与组织基础，不实现项目、合同、财务、员工和审批工作流。
