# 项目总览状态、税金与重要施工机械实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** 将项目阶段精简为五项，独立维护合同签订状态和项目税金组合，约束发票开票公司与税金选择，并从施工详情聚合重要机械到项目总览。

**Architecture:** 在领域层新增结构化枚举，在基础设施层新增 ProjectTaxConfiguration 聚合明细并扩展项目、发票和施工记录。项目工作台继续作为总览统一读取入口，财务服务统一校验发票的项目签约公司与税金配置，施工服务维护记录级总览标记；Razor 页面只提交结构化选择，不复制派生数据。

**Tech Stack:** .NET 8、ASP.NET Core Razor Pages、Entity Framework Core、SQLite、xUnit、FluentAssertions、PowerShell 7。

**Execution note:** 按仓库 AGENTS.md，在当前工作区串行执行，不创建 worktree 或子代理。数据库迁移前必须完成备份恢复校验。Git 历史操作留到用户明确授权后执行。

---

## 文件结构

- 新增 src/EngineeringManager.Domain/Projects/ContractSigningStatus.cs：合同签订状态。
- 新增 src/EngineeringManager.Domain/Finance/ProjectInvoiceType.cs：普票/专票类型。
- 新增 src/EngineeringManager.Domain/Finance/ProjectTaxRules.cs：固定税率校验。
- 新增 src/EngineeringManager.Infrastructure/Data/ProjectTaxConfiguration.cs：项目税金配置实体。
- 修改项目、发票、施工记录实体及 ApplicationDbContext。
- 修改项目、工作台、施工、财务 DTO 与服务。
- 修改项目列表、总览、详细编辑和两个发票录入入口。
- 新增 EF Core migration 并同步模型快照。
- 修改项目、财务、施工、Web 和响应式测试。

### Task 1: 领域状态与持久化模型

**Files:**

- Create: src/EngineeringManager.Domain/Projects/ContractSigningStatus.cs
- Create: src/EngineeringManager.Domain/Finance/ProjectInvoiceType.cs
- Create: src/EngineeringManager.Domain/Finance/ProjectTaxRules.cs
- Modify: src/EngineeringManager.Domain/Projects/ProjectStage.cs
- Create: src/EngineeringManager.Infrastructure/Data/ProjectTaxConfiguration.cs
- Modify: src/EngineeringManager.Infrastructure/Data/Project.cs
- Modify: src/EngineeringManager.Infrastructure/Data/InvoiceEntry.cs
- Modify: src/EngineeringManager.Infrastructure/Data/ProjectConstructionRecord.cs
- Modify: src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs
- Test: tests/EngineeringManager.Tests/Domain/ProjectTaxRulesTests.cs
- Test: tests/EngineeringManager.Tests/Infrastructure/ProjectModelTests.cs

- [ ] **Step 1: 写失败测试**

ProjectTaxRulesTests 断言仅 0.01、0.03、0.06、0.09、0.13 合法。ProjectModelTests 创建同一项目的 3% 专票与 9% 普票并成功保存，再插入重复 3% 专票并断言 DbUpdateException；同时验证合同状态和设备总览标记可持久化。

- [ ] **Step 2: 运行 RED**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectTaxRulesTests|FullyQualifiedName~ProjectModelTests"

Expected: 因新类型或属性不存在而 FAIL。

- [ ] **Step 3: 实现最小领域模型**

ProjectStage 只保留 AwaitingMobilization、UnderConstruction、Suspended、CompletedUnsettled、SettledArchived。新增 ContractSigningStatus 的 NotSigned、SentForSignature、FullySigned；新增 ProjectInvoiceType 的 Ordinary、Special。ProjectTaxRules 暴露固定税率并提供 IsAllowedRate。

Project 删除 ArchiveStatus，新增 ContractSigningStatus 与 TaxConfigurations；ProjectTaxConfiguration 包含 ProjectId、TaxRate、InvoiceType、IsActive、时间戳、并发标记；InvoiceEntry 新增可空 ProjectTaxConfigurationId 与可空结构化票种；ProjectConstructionRecord 新增 ShowInProjectOverview。

- [ ] **Step 4: 配置 EF 模型**

为 (ProjectId, TaxRate, InvoiceType) 建唯一索引；发票到税金配置 DeleteBehavior.Restrict；施工标记默认 false；配置金额精度与现有发票税率一致。

- [ ] **Step 5: 运行 GREEN**

重复 Step 2，Expected: PASS。

### Task 2: 项目创建、更新与工作台聚合

**Files:**

- Modify: src/EngineeringManager.Application/Projects/ProjectDtos.cs
- Modify: src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs
- Modify: src/EngineeringManager.Infrastructure/Projects/ProjectService.cs
- Modify: src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs
- Modify: src/EngineeringManager.Infrastructure/Projects/ProjectSummaryService.cs
- Test: tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs
- Test: tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs

- [ ] **Step 1: 写项目服务失败测试**

创建项目时提交 3% 专票和 9% 普票，断言均保存；重复组合被拒绝；合同签订状态独立更新；SettledArchived 项目仍允许修改。

期望 DTO：

    public sealed record ProjectTaxConfigurationInput(decimal TaxRate, ProjectInvoiceType InvoiceType);
    public sealed record ProjectTaxConfigurationDto(Guid Id, decimal TaxRate, ProjectInvoiceType InvoiceType, bool IsActive, Guid ConcurrencyStamp);

- [ ] **Step 2: 写工作台失败测试**

准备已勾选设备、未勾选设备和班组三条施工记录，断言总览只返回已勾选设备，并返回施工记录 ID、设备 ID、编号、名称、进退场日期。

- [ ] **Step 3: 运行 RED**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~ProjectWorkspaceServiceTests"

- [ ] **Step 4: 扩展 DTO 与项目服务**

CreateProjectRequest、UpdateProjectRequest、ProjectDto、ProjectWorkspaceOverviewDto 使用新阶段、合同状态与税金配置集合。服务集中验证固定税率、重复组合以及已使用配置只停用不删除，继续写审计和并发标记。

- [ ] **Step 5: 更新搜索与汇总**

移除 ArchiveStatus 映射和条件。列表阶段筛选只使用五项 ProjectStage；SettledProjectCount 以 SettledArchived 为结算归档口径。

- [ ] **Step 6: 运行 GREEN**

重复 Step 3，Expected: PASS。

### Task 3: 发票开票公司和税金配置强校验

**Files:**

- Modify: src/EngineeringManager.Application/Finance/FinanceDtos.cs
- Modify: src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs
- Modify: src/EngineeringManager.Application/Projects/ProjectWorkspaceDtos.cs
- Modify: src/EngineeringManager.Infrastructure/Projects/ProjectWorkspaceService.cs
- Test: tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs
- Test: tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs

- [ ] **Step 1: 写失败测试**

覆盖合法组合成功、开票公司不属于项目失败、税金配置属于其他项目失败、配置已停用失败、修改历史无配置发票时必须补选有效配置。

- [ ] **Step 2: 运行 RED**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~FinanceLedgerServiceTests"

- [ ] **Step 3: 修改请求与服务**

CreateInvoiceRequest 和 UpdateInvoiceRequest 使用 ProjectTaxConfigurationId。服务加载项目签约公司及税金配置，验证归属和启用状态，并从配置写入 TaxRate 与 InvoiceType，防止调用方伪造。历史行可空只用于读取，任何新建或修改必须选择有效配置。

- [ ] **Step 4: 更新工作台映射**

ProjectInvoiceItemDto 返回 ProjectTaxConfigurationId 与 ProjectInvoiceType?，表格仍分别显示税率与票种。

- [ ] **Step 5: 运行 GREEN**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~FinanceLedgerServiceTests|FullyQualifiedName~ProjectWorkspaceServiceTests"

Expected: PASS。

### Task 4: 施工机械总览标记

**Files:**

- Modify: src/EngineeringManager.Application/Projects/ProjectConstructionDtos.cs
- Modify: src/EngineeringManager.Infrastructure/Projects/ProjectConstructionService.cs
- Modify: src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs
- Test: tests/EngineeringManager.Tests/Application/ProjectConstructionServiceTests.cs

- [ ] **Step 1: 写失败测试**

验证设备记录可保存 ShowInProjectOverview=true，班组记录提交 true 被拒绝，自动生成的下一项目草稿标记为 false。

- [ ] **Step 2: 运行 RED**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectConstructionServiceTests"

- [ ] **Step 3: 扩展 DTO、保存和审计**

ProjectConstructionRecordDto 与 SaveProjectConstructionRecordRequest 加入 ShowInProjectOverview。服务对班组 true 抛出“施工班组不能显示在项目总览”，设备按请求保存；自动流转草稿明确 false；审计快照包含该字段。

- [ ] **Step 4: 运行 GREEN**

重复 Step 2，Expected: PASS。

### Task 5: 项目、施工与发票页面

**Files:**

- Modify: src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs
- Modify: src/EngineeringManager.Web/Pages/Projects/Index.cshtml
- Modify: src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs
- Modify: src/EngineeringManager.Web/Pages/Projects/Edit.cshtml
- Modify: src/EngineeringManager.Web/Pages/Projects/Edit.cshtml.cs
- Modify: src/EngineeringManager.Web/Pages/Projects/Details.cshtml
- Modify: src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs
- Modify: src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml
- Modify: src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml.cs
- Modify:现有项目 CSS/JS 资源
- Test: tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs
- Test: tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs
- Test: tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs

- [ ] **Step 1: 写页面失败测试**

断言页面不再包含“归档状态”，包含“合同签订”“税金”“施工机械”“显示在项目总览”；项目备注节点位于总览最后并有全宽类；发票表单包含开票公司和税金组合。

- [ ] **Step 2: 运行 RED**

    $ErrorActionPreference = 'Stop'
    dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ResponsiveUiAssetTests"

- [ ] **Step 3: 更新项目页面**

项目阶段只显示五项；合同状态使用下拉框；税金使用 5×2 勾选矩阵；重要机械按施工记录显示编号、名称、进场和退场/仍在场，并链接 tab=construction 与 recordId；备注移动到 detail-grid 最后并独占整行。

- [ ] **Step 4: 更新施工详情**

仅设备类型显示总览复选框，列表显示总览状态。从总览进入时使用记录 ID 定位和高亮，班组类型隐藏并清空控件。

- [ ] **Step 5: 更新两个发票入口**

项目原位编辑与财务详细录入均按项目显示签约公司和启用税金配置；税金文本为“3% · 专票”；发票列表分别显示公司、税率和票种。

- [ ] **Step 6: 更新响应式样式并运行 GREEN**

税金矩阵、重要机械和备注在 390px 不横向溢出。重复 Step 2，Expected: PASS。

### Task 6: 数据库迁移与旧数据映射

**Files:**

- Create: src/EngineeringManager.Infrastructure/Data/Migrations/ 下由 EF 生成的 ProjectOverviewStatusTaxEquipment migration 与 designer
- Modify: src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs
- Test: tests/EngineeringManager.Tests/Infrastructure/ApplicationDbContextTests.cs

- [ ] **Step 1: 运行备份恢复校验**

Workdir: EngineeringManager

    $ErrorActionPreference = 'Stop'
    & '.\scripts\verify-backup-restore.ps1'

Expected: 成功退出并确认备份可恢复。

- [ ] **Step 2: 添加迁移映射断言**

覆盖旧阶段整数、旧归档状态覆盖、合同存在性初始化和施工标记默认 false。

- [ ] **Step 3: 生成迁移**

    $ErrorActionPreference = 'Stop'
    dotnet ef migrations add ProjectOverviewStatusTaxEquipment --project src/EngineeringManager.Infrastructure --startup-project src/EngineeringManager.Web --context ApplicationDbContext

- [ ] **Step 4: 修订迁移顺序**

先增加新字段和表，再用 SQL 完成阶段映射与合同状态初始化，最后删除 ArchiveStatus。历史发票只转换可识别票种，未知值保持空；现有机械标记默认 false。

- [ ] **Step 5: 验证新数据库应用迁移**

    $ErrorActionPreference = 'Stop'
    dotnet ef database update --project src/EngineeringManager.Infrastructure --startup-project src/EngineeringManager.Web --context ApplicationDbContext --connection 'Data Source=artifacts/project-overview-migration-check.db'

Expected: 所有迁移成功应用。

### Task 7: 全量验证与页面验收

- [ ] **Step 1: 编译**

    $ErrorActionPreference = 'Stop'
    dotnet build EngineeringManager.sln --no-restore

- [ ] **Step 2: 全量测试**

    $ErrorActionPreference = 'Stop'
    dotnet test EngineeringManager.sln --no-build

- [ ] **Step 3: 运行仓库现有质量门禁**

先从 README、AGENTS 或 scripts 目录确定已有门禁命令，再逐项执行，不自行发明替代命令。

- [ ] **Step 4: 启动服务并做桌面与 390px 验收**

验证项目总览、详细编辑、施工详情、发票新增和原位修改；检查备注末行、税金矩阵、机械链接、错误提示及横向溢出。

- [ ] **Step 5: 检查工作区**

    $ErrorActionPreference = 'Stop'
    git diff --check
    git status --short

不得把迁移验证数据库、日志或临时截图加入工作区。未经用户明确授权不提交或推送。
