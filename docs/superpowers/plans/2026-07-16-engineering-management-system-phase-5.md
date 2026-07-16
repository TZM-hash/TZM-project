# 阶段 5：员工、工资与员工往来实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现正式员工和劳务员工档案、时间化归属历史、阶段工资批次、组合工资项目、项目成本分摊、多次实际支付，以及简单报销、借支、分红/利息和其他员工往来。

**Architecture:** Domain 保存工资项目性质、应发/已发/未发和往来余额计算规则；Application 将员工、工资和员工往来拆为清晰服务接口；Infrastructure 使用 EF Core 保存档案、归属、工资批次、工资项目、成本分摊和实际支付，并复用阶段 4 资金账户流水；Web 提供员工总览、工资批次和员工往来入口。工资应付与实际支付分离，分红/利息不建立股权档案。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：员工、工资和往来领域规则

**Files:**
- Create: `src/EngineeringManager.Domain/Employees/EmployeeEnums.cs`
- Create: `src/EngineeringManager.Domain/Employees/PayrollCalculator.cs`
- Create: `src/EngineeringManager.Domain/Employees/EmployeeLedgerCalculator.cs`
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `tests/EngineeringManager.Tests/Domain/PayrollCalculatorTests.cs`
- Create: `tests/EngineeringManager.Tests/Domain/EmployeeLedgerCalculatorTests.cs`

- [ ] 写测试，确认工资项目按收入/扣款计算应发，应发减实际支付得到未发，超发保留并标记风险。
- [ ] 写测试，确认借支发放、归还、工资抵扣和报销/分红/利息应付支付的余额口径。
- [ ] 运行测试确认领域类型尚不存在而失败。
- [ ] 实现员工类型、工资批次类型、工资项目类型、收款人类型、往来类型和纯计算规则。
- [ ] 新增员工查看/管理、工资查看/管理和员工往来管理权限键。
- [ ] 运行领域测试确认通过。

## Task 2：员工档案、归属历史和智能复制

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Employee.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/EmployeeAffiliationHistory.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `src/EngineeringManager.Application/Employees/EmployeeDtos.cs`
- Create: `src/EngineeringManager.Application/Employees/IEmployeeService.cs`
- Create: `src/EngineeringManager.Infrastructure/Employees/EmployeeService.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/EmployeeModelTests.cs`
- Create: `tests/EngineeringManager.Tests/Application/EmployeeServiceTests.cs`

- [ ] 写 SQLite 测试，确认正式/劳务员工、部门、项目、施工班组、签约公司和时间区间归属可保存。
- [ ] 写服务测试，确认员工编号唯一、归属区间有效且同一主归属不重叠。
- [ ] 写复制测试，确认复制保留员工类型、岗位和默认工资设置，清除身份证、银行卡、历史归属、工资和附件，新编号/姓名由用户指定。
- [ ] 运行测试确认模型和服务尚不存在而失败。
- [ ] 实现员工档案、归属历史、列表、详情、创建和智能复制。
- [ ] 运行模型和服务测试确认通过。

## Task 3：工资批次、组合工资项目和成本分摊

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/PayrollBatch.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PayrollItem.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PayrollCostAllocation.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PayrollPayment.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `src/EngineeringManager.Application/Payroll/PayrollDtos.cs`
- Create: `src/EngineeringManager.Application/Payroll/IPayrollService.cs`
- Create: `src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/PayrollModelTests.cs`
- Create: `tests/EngineeringManager.Tests/Application/PayrollServiceTests.cs`

- [ ] 写测试，确认自然月、任意区间、项目阶段、工程节点和临时结算批次可保存。
- [ ] 写测试，确认同一员工同一批次可组合月薪、日/小时、计件、包干、加班、奖金、补贴、扣款、补发和冲减。
- [ ] 写测试，确认工资项目可按金额分摊到多个项目/签约公司，分摊合计必须等于项目金额。
- [ ] 运行测试确认模型和服务尚不存在而失败。
- [ ] 实现批次、工资项目、成本分摊、确认状态和员工/批次/项目汇总。
- [ ] 运行工资模型和服务测试确认通过。

## Task 4：工资实际支付和资金流水

**Files:**
- Modify: `src/EngineeringManager.Domain/Finance/FinanceEnums.cs`
- Modify: `src/EngineeringManager.Application/Payroll/PayrollDtos.cs`
- Modify: `src/EngineeringManager.Application/Payroll/IPayrollService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Payroll/PayrollService.cs`
- Create: `tests/EngineeringManager.Tests/Application/PayrollPaymentTests.cs`

- [ ] 写测试，确认工资可分多次、现金/银行/微信/支付宝等渠道支付。
- [ ] 写测试，确认收款人可以是员工本人、班组负责人或受托收款人，并保存姓名和关联单位。
- [ ] 写测试，确认工资支付自动生成账户流出，应发/已发/未发和超发风险自动更新。
- [ ] 运行测试确认支付行为尚未实现而失败。
- [ ] 实现工资支付事务和账户流水，禁止修改账户余额字段。
- [ ] 运行支付测试确认工资记录与资金流水一致。

## Task 5：报销、借支、分红/利息和其他往来

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/ExpenseRecord.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/EmployeeAdvance.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/EmployeeOtherPayment.cs`
- Create: `src/EngineeringManager.Application/EmployeeLedger/EmployeeLedgerDtos.cs`
- Create: `src/EngineeringManager.Application/EmployeeLedger/IEmployeeLedgerService.cs`
- Create: `src/EngineeringManager.Infrastructure/EmployeeLedger/EmployeeLedgerService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Application/EmployeeLedgerServiceTests.cs`

- [ ] 写测试，确认简单报销保存员工、项目、部门、签约公司、类别、应付、支付、退回和冲销。
- [ ] 写测试，确认借支发放生成账户流出，归还生成账户流入，工资抵扣不重复生成现金流水。
- [ ] 写测试，确认分红、利息和其他往来只登记每次应付/支付，不保存持股比例或股权变更。
- [ ] 运行测试确认往来服务尚不存在而失败。
- [ ] 实现员工往来记录、实际支付/收回和员工余额汇总。
- [ ] 运行往来测试确认台账与账户流水一致。

## Task 6：员工、工资和往来页面与授权

**Files:**
- Modify: `src/EngineeringManager.Web/Program.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Employees/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Employees/Create.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Employees/Create.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Payroll/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/EmployeeLedger/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/EmployeeLedger/Index.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Web/EmployeePayrollAuthorizationTests.cs`

- [ ] 写 Web 测试，确认财务和管理员可管理工资/往来，应用管理员可管理员工，查询人员只读，项目负责人和现场人员不能录入工资。
- [ ] 运行测试确认页面尚不存在而失败。
- [ ] 实现员工类型指标卡、工资应发/已发/未发指标、员工往来余额和响应式明细表。
- [ ] 页面只调用应用服务，不直接计算工资、余额或资金流水。
- [ ] 运行 Web 测试确认权限边界和页面响应通过。

## Task 7：迁移、质量门禁和进度更新

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_EmployeesPayrollLedger.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 运行完整质量门禁，确认 Release 构建 0 警告、全部测试通过。
- [ ] 创建并应用阶段 5 EF Migration 到本机 SQL Server。
- [ ] 真实启动 Web，验证员工/工资页面匿名边界、健康检查和数据库就绪状态。
- [ ] 更新唯一进度文件，记录迁移名称、测试数量、工资口径、遗留风险和阶段 6 计划。

## 阶段 5 完成定义

- 员工明确区分正式员工和劳务员工，归属变化保留时间历史。
- 工资批次支持任意日期区间和阶段性结算，同一员工可组合多个工资项目并分摊项目成本。
- 工资应付与实际支付分离，支持多次、多渠道和代收款人，支付自动进入账户流水。
- 报销、借支、归还、分红/利息和其他往来可追溯，但不建设完整费用报销审批或股权管理系统。
- 员工复制不复制身份证、银行卡、历史归属、工资、支付、往来和附件。
