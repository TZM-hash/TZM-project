# 工资发放批次、施工班组与临时人员统一数据源实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有员工年度总账之上建立一次付款一批次的统一工资发放数据源，并让员工、施工班组、临时人员、项目财务和账户流水从同一人员明细追溯与汇总。

**Architecture:** 扩展现有 `PayrollBatch` 作为批次头，扩展现有 `PayrollPayment` 作为员工、班组人员和临时人员共用的唯一金额明细。班组工程款通过人员明细实时聚合，`PayrollCrewAllocation` 只保存合同/应付关联而不保存金额；一个新批次只生成一笔账户流水。员工年度总账直接读取员工类型批次明细作为领款来源，不复制到 `EmployeeReceipt`。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、Entity Framework Core、SQL Server、xUnit、FluentAssertions、PowerShell 7。

**Execution constraints:** 串行 TDD；不创建子代理或 worktree；不执行 Git 暂存、提交或推送；所有 PowerShell 命令以 `$ErrorActionPreference = 'Stop'` 开头；迁移前执行现有安全备份流程。

---

## 文件结构

### 新增领域与数据文件

- `EngineeringManager/src/EngineeringManager.Domain/Employees/PayrollDisbursementRules.cs`：统一明细金额、人员引用和核对规则。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ConstructionWorker.cs`：班组人员轻量主档。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ConstructionCrewMembership.cs`：班组人员时间化归属。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/TemporaryWorker.cs`：临时人员档案。
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollCrewAllocation.cs`：班组合同/应付关联，不保存金额。

### 新增应用与服务文件

- `EngineeringManager/src/EngineeringManager.Application/ConstructionCrews/ConstructionCrewDtos.cs`
- `EngineeringManager/src/EngineeringManager.Application/ConstructionCrews/IConstructionCrewService.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/ConstructionCrews/ConstructionCrewService.cs`
- `EngineeringManager/src/EngineeringManager.Application/TemporaryWorkers/TemporaryWorkerDtos.cs`
- `EngineeringManager/src/EngineeringManager.Application/TemporaryWorkers/ITemporaryWorkerService.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/TemporaryWorkers/TemporaryWorkerService.cs`

### 新增页面

- `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Details.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Details.cshtml.cs`

### 主要修改文件

- `EngineeringManager/src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`
- `EngineeringManager/src/EngineeringManager.Domain/Finance/FinanceEnums.cs`
- `EngineeringManager/src/EngineeringManager.Application/Payroll/PayrollDtos.cs`
- `EngineeringManager/src/EngineeringManager.Application/Payroll/IPayrollService.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollBatch.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/EmployeeAnnualLedger/EmployeeAnnualLedgerService.cs`
- `EngineeringManager/src/EngineeringManager.Application/EmployeeAnnualLedger/EmployeeAnnualLedgerDtos.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Index.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml.cs`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`
- `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- `docs/开发进度.md`

---

### Task 1: 领域规则与枚举

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Domain/Employees/PayrollDisbursementRules.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Domain/PayrollDisbursementRulesTests.cs`

- [ ] **Step 1: 写失败测试**

测试以下行为：三类人员引用必须互斥；金额必须大于零；批次核对时明细合计必须等于实际总额；包含班组人员时项目必填；同一人员键不能重复。

- [ ] **Step 2: 运行测试确认 RED**

```powershell
$ErrorActionPreference = 'Stop'
& 'C:\Users\TZM-NEW\.dotnet10\dotnet.exe' test 'EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj' -c Release --filter 'FullyQualifiedName~PayrollDisbursementRulesTests'
```

预期：因缺少枚举和规则类型而失败。

- [ ] **Step 3: 最小实现**

增加 `PayrollRecipientType`、`PayrollBatchStatus.ModifiedPendingReview` 和纯领域校验/汇总类型。规则只接收值对象，不依赖 EF Core。

- [ ] **Step 4: 运行定向测试确认 GREEN**

预期：新增领域测试全部通过。

### Task 2: 持久化模型与数据库约束

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ConstructionWorker.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ConstructionCrewMembership.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/TemporaryWorker.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollCrewAllocation.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollBatch.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/PayrollDisbursementModelTests.cs`

- [ ] **Step 1: 写 SQLite 持久化失败测试**

覆盖混合批次、三类人员、班组归属、临时人员、班组关联和唯一账户流水字段持久化；覆盖同批次同人员唯一约束和人员类型检查约束。

- [ ] **Step 2: 运行测试确认 RED**

预期：缺少实体、DbSet 和模型配置。

- [ ] **Step 3: 实现实体和 EF 配置**

扩展旧字段为兼容可空字段；新批次使用批次级日期、账户和流水。为员工、班组人员、临时人员分别建立过滤唯一索引；为班组角色建立外键但在服务层验证 `ConstructionCrew` 角色。

- [ ] **Step 4: 运行模型测试确认 GREEN**

### Task 3: 施工班组和临时人员服务

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/ConstructionCrews/ConstructionCrewDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/ConstructionCrews/IConstructionCrewService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/ConstructionCrews/ConstructionCrewService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/TemporaryWorkers/TemporaryWorkerDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/TemporaryWorkers/ITemporaryWorkerService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/TemporaryWorkers/TemporaryWorkerService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ConstructionCrewServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/TemporaryWorkerServiceTests.cs`

- [ ] **Step 1: 写服务失败测试**

班组服务覆盖：只列出施工班组角色、人员新增、退出、转组、主要归属不重叠、历史批次快照不变、按日期展开人员发放。

临时人员服务覆盖：身份证选填、疑似重复提示但不阻止、停用、转为员工关联、发放历史查询。

- [ ] **Step 2: 运行定向测试确认 RED**

- [ ] **Step 3: 实现 DTO、接口和服务**

所有敏感字段 DTO 通过 `EmployeeSensitiveDataMasker` 或同等服务端规则脱敏；修改主档记录 `AuditLog`。

- [ ] **Step 4: 运行服务测试确认 GREEN**

### Task 4: 统一工资批次服务

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Payroll/PayrollDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Payroll/IPayrollService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/PayrollDisbursementServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/PayrollPaymentTests.cs`

- [ ] **Step 1: 写混合批次失败测试**

构造员工、两个施工班组、班组人员和临时人员，验证保存草稿、核对差额、项目必填、账户归属、重复人员拒绝、快照保存和各类小计。

- [ ] **Step 2: 写唯一流水失败测试**

验证一个新统一批次无论包含多少人员，都只生成一个 `AccountTransaction`；修改总额、公司或账户时更新同一来源流水且不重复新增。

- [ ] **Step 3: 运行定向测试确认 RED**

- [ ] **Step 4: 重构服务为整体保存事务**

新增 `SaveBatchAsync(userId, request)`、`GetBatchAsync`、筛选总览、作废和重新核对能力。服务一次加载并比较全部旧明细，保存批次头、明细、班组关联、账户流水和完整审计快照。

- [ ] **Step 5: 运行工资服务测试确认 GREEN**

### Task 5: 年度总账与项目财务集成

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/EmployeeAnnualLedger/EmployeeAnnualLedgerDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/EmployeeAnnualLedger/EmployeeAnnualLedgerService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Finance/FinanceEnums.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/EmployeeAnnualLedgerAggregationTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/PayrollDisbursementFinanceTests.cs`

- [ ] **Step 1: 写年度总账防重复测试**

验证员工统一批次明细作为领款进入年度总账，并返回批次/明细来源 ID；同一金额不出现在 `EmployeeReceipt`；旧 `PayrollPayment` 继续读取。

- [ ] **Step 2: 写财务聚合测试**

验证员工和临时人员进入项目人工成本，班组人员按班组进入项目已付款；班组工程款不新增第二笔账户流水；合同/应付关联不保存金额。

- [ ] **Step 3: 运行定向测试确认 RED**

- [ ] **Step 4: 实现查询聚合和来源链接**

在财务汇总中联合普通付款和工资代发班组工程款，保持账户余额只读取唯一账户流水。

- [ ] **Step 5: 运行集成测试确认 GREEN**

### Task 6: 工资、施工班组和临时人员页面

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Payroll/Edit.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Crews/Details.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Details.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/TemporaryWorkers/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/PayrollDisbursementPageTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/ConstructionCrewPageTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/TemporaryWorkerPageTests.cs`

- [ ] **Step 1: 写页面与授权失败测试**

检查导航、页面路由、表单字段、混合人员区域、差额摘要、批次定位参数、班组名册和临时人员发放历史；匿名和无权限用户不能修改。

- [ ] **Step 2: 运行 Web 测试确认 RED**

- [ ] **Step 3: 实现 Razor Pages**

遵循现有分区表单、数据工作台、按钮和返回样式。批次编辑使用单次整体提交；服务端始终重新计算金额，前端实时合计只作为交互反馈。

- [ ] **Step 4: 运行 Web 测试确认 GREEN**

### Task 7: 员工详情和跨模块追溯

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Finance/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Finance/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeAnnualLedgerPageTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Web/FinanceAuthorizationTests.cs`

- [ ] **Step 1: 写来源跳转失败测试**

员工领款、班组工程款、班组人员、临时人员和项目财务必须输出 `/Payroll/Edit?id=<batchId>&lineId=<lineId>&returnUrl=...` 形式的来源链接。

- [ ] **Step 2: 运行测试确认 RED**

- [ ] **Step 3: 实现来源 DTO 和页面链接**

批次编辑页按 `lineId` 输出定位属性；保存和取消均安全返回站内 `returnUrl`。

- [ ] **Step 4: 运行追溯测试确认 GREEN**

### Task 8: 权限、导出、样例数据和注册

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataBuilder.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Development/SampleDataAssertions.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Application/ModuleExportTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/DevelopmentSampleDataSeederTests.cs`
- Test: `EngineeringManager/tests/EngineeringManager.Tests/Security/FinalAuthorizationMatrixTests.cs`

- [ ] **Step 1: 写权限和导出失败测试**

覆盖工资、班组、临时人员权限；默认导出敏感字段脱敏；工资批次和班组工程款导出来自统一明细。

- [ ] **Step 2: 写样例数据失败测试**

要求至少一个员工、两个班组和临时人员混合批次，明细合计等于批次实际总额，且只有一笔账户流水。

- [ ] **Step 3: 运行测试确认 RED**

- [ ] **Step 4: 实现权限、注册、导出和样例数据**

- [ ] **Step 5: 运行定向测试确认 GREEN**

### Task 9: 数据库备份与迁移

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/20260718090000_UnifiedPayrollDisbursementCrewsTemporaryWorkers.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/20260718090000_UnifiedPayrollDisbursementCrewsTemporaryWorkers.Designer.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: 运行现有安全备份**

```powershell
$ErrorActionPreference = 'Stop'
& 'EngineeringManager/scripts/verify-backup-restore.ps1'
```

预期：SQL Server CHECKSUM 备份和恢复核验通过。

- [ ] **Step 2: 生成迁移**

使用用户级 .NET 10 SDK 和项目既有 dotnet-ef 工具生成迁移，不手写模型快照。

- [ ] **Step 3: 检查迁移 SQL**

确认旧 `PayrollPayments` 数据不丢失；旧必填外键转可空；新检查约束只对统一新记录生效；不执行破坏性删除。

- [ ] **Step 4: 应用迁移到 Development 测试库**

- [ ] **Step 5: 运行模型与服务回归测试**

### Task 10: 全量验证、浏览器验收与进度文档

**Files:**
- Modify: `docs/开发进度.md`

- [ ] **Step 1: 运行 Release 全量测试**

```powershell
$ErrorActionPreference = 'Stop'
& 'C:\Users\TZM-NEW\.dotnet10\dotnet.exe' test 'EngineeringManager/EngineeringManager.sln' -c Release
```

预期：全部测试通过，0 失败，0 跳过。

- [ ] **Step 2: 运行质量门禁**

```powershell
$ErrorActionPreference = 'Stop'
& 'EngineeringManager/scripts/quality-gate.ps1'
```

预期：`QUALITY_GATE=PASS`。

- [ ] **Step 3: 浏览器验收**

使用 Development 测试管理员验证：工资批次混合录入、差额校验、员工追溯、班组历史展开、临时人员历史、项目财务聚合、账户唯一流水、管理员修改及审计、敏感信息脱敏、桌面和 390px 手机布局。

- [ ] **Step 4: 检查浏览器控制台和响应式溢出**

预期：无 JavaScript 错误；桌面和手机 `scrollWidth == clientWidth`；触控目标符合现有 44px 规则。

- [ ] **Step 5: 更新开发进度**

记录设计文档、实施计划、迁移名称、测试数量、浏览器结果、风险边界和未执行 Git 操作。

- [ ] **Step 6: 最终自审**

对照设计逐项确认没有金额副本、重复账户流水、年度总账重复领款、未脱敏敏感字段或失效来源链接。

## 执行结果（2026-07-18）

- Task 1～10 已按顺序完成；金额只在统一工资批次与人员明细中维护。
- 安全备份恢复核验通过；迁移 `20260718020604_UnifiedPayrollCrewTemporaryWorkers` 已应用到 `EngineeringManager_Test`。
- 旧工资发放记录全部保留并按员工类型兼容；统一批次只生成一笔原始流出，作废使用独立冲销流水。
- Release 全量测试 407/407；发布与仓库质量门禁均通过。
- Playwright 验证混合批次、班组两人倒查、精确明细定位、控制台与 390px 响应式；未执行 Git 暂存、提交或推送。
