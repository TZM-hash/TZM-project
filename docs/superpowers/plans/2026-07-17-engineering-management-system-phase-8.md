# 阶段 8：自有公司管理与公司维度统计实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 `LegalEntity`、项目和财务模型上补齐自有公司档案、多账户默认用途、证照、公司维度统计、权限和 Excel 数据交换，不建设公司内部往来台账。

**Architecture:** 继续以 `LegalEntity` 作为唯一公司主体，新增可维护组合分类和证照实体；现有所有带 `LegalEntityId` 的合同、财务、工资和项目数据仍是公司统计权威来源。应用层新增聚合服务，页面只负责输入和展示；导入导出、提醒、审计和权限沿用现有公共能力。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、ASP.NET Core Identity、现有 SimpleXlsx、xUnit、FluentAssertions、SQLite 测试数据库。

---

## 文件结构

- `Domain/Organization/CompanyCategory.cs`：管理员可维护的组合分类。
- `Infrastructure/Data/CompanyCertificate.cs`：公司证照及有效期。
- `Application/Companies/*`：公司档案、账户、证照和总览 DTO/接口。
- `Infrastructure/Companies/CompanyManagementService.cs`：公司业务与聚合实现。
- `Web/Pages/Companies/*`：公司列表、详情、编辑、证照和图形化总览。
- 现有 `OrganizationService` 继续负责组织单元，不复制公司统计逻辑。

## Task 1：组合分类、公司档案和默认账户规则

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Domain/Organization/CompanyCategory.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Organization/LegalEntity.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/FinancialAccount.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Domain/CompanyRulesTests.cs`

- [ ] 写失败测试，固定分类停用后历史仍可引用、公司组合分类必填，以及同一公司每种默认账户用途最多一个。

```csharp
[Fact]
public void DefaultAccountPurposesMustBeUniquePerCompany()
{
    var accounts = new[]
    {
        new CompanyAccountDefault(true, false, false),
        new CompanyAccountDefault(true, false, false)
    };
    Action action = () => CompanyAccountRules.Validate(accounts);
    action.Should().Throw<InvalidOperationException>();
}
```

- [ ] 运行 `dotnet test --filter FullyQualifiedName~CompanyRulesTests`，确认因类型不存在而 RED。
- [ ] 实现 `CompanyCategory`、公司法人/经营者、地址、电话、组合分类外键及账户默认用途标记；唯一性最终由数据库过滤索引保证。
- [ ] 重跑测试确认 GREEN。

## Task 2：公司证照与 EF Core 数据约束

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/CompanyCertificate.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/CompanyModelTests.cs`

- [ ] 写 SQLite 失败测试，验证分类代码唯一、证照关联公司、账户默认用途唯一、公司有业务引用时使用 Restrict、证照逻辑删除保留历史。
- [ ] 添加 `DbSet<CompanyCategory>`、`DbSet<CompanyCertificate>` 和最大长度、索引、外键、并发标记配置。

```csharp
entity.HasIndex(x => new { x.LegalEntityId, x.IsDefaultCollection })
    .IsUnique()
    .HasFilter("[IsDefaultCollection] = 1");
```

- [ ] 重跑 `CompanyModelTests` 并执行 `dotnet build --configuration Release`。

## Task 3：公司管理与聚合应用服务

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Companies/CompanyDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Companies/ICompanyManagementService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Companies/CompanyManagementService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/CompanyManagementServiceTests.cs`

- [ ] 写失败测试：公司增改停用、组合分类维护、整条复制清空唯一字段、多个账户默认用途、证照到期和权限数据范围。
- [ ] 写聚合测试：合同额、当前暂估/结算、应收/收款、开票、应付/付款、工资和账户余额按公司正确汇总，全公司汇总不把账户转账当经营收支。
- [ ] 定义明确接口。

```csharp
public interface ICompanyManagementService
{
    Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken token);
    Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken token);
    Task<CompanyDetailsDto> SaveAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken token);
    Task<CompanyDetailsDto> CopyAsync(CompanyActor actor, Guid sourceId, CancellationToken token);
    Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken token);
}
```

- [ ] 实现服务端权限重校验、规范化、并发标记、逻辑删除和聚合查询，注册 DI。
- [ ] 重跑服务测试确认 GREEN。

## Task 4：权限、管理员创建用户和关闭公开注册

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Areas/Identity/Pages/Account/Register.cshtml.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/CompanyAuthorizationTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/HomePageTests.cs`

- [ ] 写失败测试，确认公司管理权限、公司数据范围、公开 `/Identity/Account/Register` 返回 404/禁止、管理员用户管理仍可创建账号。
- [ ] 新增 `companies.read`、`companies.manage`、`companies.certificates.manage` 权限并加入管理员/财务/查询角色的合理默认集合。
- [ ] 使用本地覆盖页禁用公开注册，不改变管理员用户管理服务。
- [ ] 运行授权测试确认 GREEN。

## Task 5：公司页面和响应式总览

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Details.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Edit.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Companies/Edit.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/CompanyPageTests.cs`

- [ ] 写页面测试，确认指标挂载点、直接显示金额、公司筛选、证照和账户区域、复制入口及匿名/越权保护。
- [ ] 实现列表、详情、编辑和指标卡/水平条形图；保持 Razor + HTML/CSS，不新增图表依赖。
- [ ] 在主导航增加“自有公司”，手机端无横向溢出。
- [ ] 运行页面测试确认 GREEN。

## Task 6：公司 Excel 导入导出与证照提醒

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/DataExchange/DataExchangeEnums.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Reminders/ReminderService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Reminders/ReminderEnums.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/CompanyDataExchangeTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ReminderServiceTests.cs`

- [ ] 写失败测试，覆盖公司、账户、证照标准模板预览/导入、自由字段导出、上次设置、个人/共享模板和证照到期提醒去重/解决。
- [ ] 增加 `Companies`、`CompanyAccounts`、`CompanyCertificates` 数据集和字段目录，沿用逐行逐列错误报告。
- [ ] 增加 `CompanyCertificateExpiring` 提醒，只对有效且设置有效期的证照生成。
- [ ] 重跑数据交换与提醒测试确认 GREEN。

## Task 7：测试数据库、迁移和阶段验收

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_CompanyManagement.cs`
- Modify: `EngineeringManager/README.md`
- Modify: `docs/开发进度.md`

- [ ] 创建 `EngineeringManager_Test` 连接配置，仅在 Development/自动验收命令中使用；确认生产配置不含测试凭据。
- [ ] 生成 `CompanyManagement` Migration 并应用到 `EngineeringManager_Test`，检查新表、列和唯一索引。
- [ ] 运行阶段 8 定向测试和完整 `scripts/quality-gate.ps1`，要求全部通过、Release 0 警告/0 错误。
- [ ] 真实启动 Release Web，验证健康端点、公开注册关闭、公司页面匿名跳转和静态资源。
- [ ] 浏览器验证桌面和 390px 公司列表/详情，无业务数据匿名泄露、无控制台错误。
- [ ] 更新 README、总体设计和唯一进度文档，记录迁移、测试数、范围边界和阶段 9 下一步。
- [ ] 本地提交阶段 8，不推送远端。

## 阶段 8 完成定义

- 现有 `LegalEntity` 成为完整但轻量的自有公司档案，且没有第二套公司主体或内部往来台账。
- 多账户默认用途、证照、权限、公司维度汇总、图形化页面和 Excel 数据交换可用。
- 公开注册关闭，Migration 只应用到 `EngineeringManager_Test`。
- 完整质量门禁、真实 HTTP/浏览器验收通过，唯一进度文档已更新并完成本地提交。
