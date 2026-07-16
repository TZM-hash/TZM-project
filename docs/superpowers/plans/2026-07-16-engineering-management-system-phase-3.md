# 阶段 3：合作单位与阶段成果实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现统一合作单位档案、多角色标签、跨项目参与关系，以及轻量阶段成果、工程量累计、验收/完工资料和附件元数据关联。

**Architecture:** Domain 保存合作单位角色、阶段成果状态和工程量累计规则；Application 提供合作单位与阶段成果服务接口；Infrastructure 使用 EF Core 持久化单位、联系人、角色、项目关系、阶段成果、阶段工程量和附件元数据；Web 提供合作单位列表/详情和阶段成果列表/详情。财务统计只预留关系，不提前实现应付、付款和发票。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、现有 `IFileStore`、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：合作单位角色与阶段工程量规则

**Files:**
- Create: `src/EngineeringManager.Domain/Partners/BusinessPartnerRoleType.cs`
- Create: `src/EngineeringManager.Domain/StageResults/StageResultType.cs`
- Create: `src/EngineeringManager.Domain/StageResults/StageResultStatus.cs`
- Create: `src/EngineeringManager.Domain/StageResults/QualityResult.cs`
- Create: `src/EngineeringManager.Domain/StageResults/StageQuantityCalculator.cs`
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `tests/EngineeringManager.Tests/Domain/StageQuantityCalculatorTests.cs`

- [ ] 写测试，确认本期量、历史累计量和目标量计算累计量、剩余量、完成比例及超量标记。
- [ ] 运行测试确认领域类型尚不存在而失败。
- [ ] 实现角色枚举、阶段成果状态和纯工程量计算器；增加合作单位和阶段成果权限键。
- [ ] 运行领域测试确认通过。

## Task 2：持久化合作单位、阶段成果和附件元数据

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/BusinessPartner.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/BusinessPartnerRole.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PartnerContact.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ProjectPartner.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/StageResult.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/StageResultLine.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/Attachment.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/AttachmentCategory.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/Contract.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/PartnerStageResultModelTests.cs`

- [ ] 写 SQLite 测试，确认一个单位多角色、联系人、跨项目参与、阶段成果工程量和附件关联可以保存。
- [ ] 运行测试确认模型尚不存在而失败。
- [ ] 添加实体、导航、唯一索引、并发标记、逻辑删除和附件安全元数据字段。
- [ ] 运行模型测试确认持久化通过。

## Task 3：合作单位服务、复制和项目关联

**Files:**
- Create: `src/EngineeringManager.Application/Partners/PartnerDtos.cs`
- Create: `src/EngineeringManager.Application/Partners/IBusinessPartnerService.cs`
- Create: `src/EngineeringManager.Infrastructure/Partners/BusinessPartnerService.cs`
- Create: `tests/EngineeringManager.Tests/Application/BusinessPartnerServiceTests.cs`

- [ ] 写服务测试，确认多角色单位只创建一条主档，编号重复被拒绝，复制不复制联系人、项目历史、附件和唯一身份信息。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现新增、列表、详情、项目关联和安全复制服务。
- [ ] 运行服务测试确认多角色和复制边界通过。

## Task 4：阶段成果、累计工程量和附件服务

**Files:**
- Create: `src/EngineeringManager.Application/StageResults/StageResultDtos.cs`
- Create: `src/EngineeringManager.Application/StageResults/IStageResultService.cs`
- Create: `src/EngineeringManager.Infrastructure/StageResults/StageResultService.cs`
- Create: `tests/EngineeringManager.Tests/Application/StageResultServiceTests.cs`

- [ ] 写服务测试，确认阶段成果按历史已记录结果累计工程量，草稿不进入累计，超过目标量时保留数据并标记风险。
- [ ] 写测试，确认附件元数据关联阶段成果但不公开真实服务器路径。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现草稿/记录、工程量累计、验收质量结果、附件元数据和项目范围查询。
- [ ] 运行服务测试确认累计口径与附件边界通过。

## Task 5：合作单位和阶段成果页面

**Files:**
- Modify: `src/EngineeringManager.Web/Program.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/StageResults/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/StageResults/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/StageResults/Create.cshtml`
- Create: `src/EngineeringManager.Web/Pages/StageResults/Create.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Web/PartnerStageResultAuthorizationTests.cs`

- [ ] 写 Web 测试，确认查询人员可读合作单位和阶段成果，现场人员可进入阶段成果提交页但不能管理合作单位，项目负责人可管理两者。
- [ ] 运行测试确认页面尚不存在而失败。
- [ ] 注册服务并实现中文列表、角色标签、阶段工程量状态和移动端友好提交入口。
- [ ] 页面只调用应用服务，不直接计算累计工程量或暴露附件路径。
- [ ] 运行 Web 测试确认权限边界和页面响应通过。

## Task 6：迁移、质量门禁和进度更新

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_PartnersStageResultsAttachments.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 运行完整质量门禁，确认 Release 构建 0 警告、全部测试通过。
- [ ] 创建并应用阶段 3 EF Migration 到本机 SQL Server。
- [ ] 真实启动 Web，验证合作单位/阶段成果未登录边界、健康检查和数据库就绪状态。
- [ ] 更新唯一进度文件，记录迁移名称、测试数量、累计口径、遗留风险和阶段 4 计划。

## 阶段 3 完成定义

- 同一合作单位可拥有多个角色，并可跨项目参与。
- 合作单位复制会清除唯一编号、联系人、项目历史、附件和审计记录。
- 阶段成果支持草稿与正式记录；只有正式记录进入累计工程量。
- 阶段成果附件只保存安全元数据和存储名，不向页面暴露服务器物理路径。
- 不建设高频施工日志、供应商登录、财务应付/付款或复杂审批工作流。
