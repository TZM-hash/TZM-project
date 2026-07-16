# 阶段 4：内部经营财务台账实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现不依赖会计科目的内部经营台账，分离应收/应付义务、实际收付资金、退款冲销、扣款、发票、内部账户和转账。

**Architecture:** Domain 保存收付汇总、风险和发票金额校验规则；Application 提供财务录入和项目经营汇总接口；Infrastructure 使用 EF Core 持久化业务单据与账户流水，收付款和内部转账在事务中自动生成资金流水；Web 提供内账总览、账户和录入入口。员工工资和员工往来留给阶段 5。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：收付款、发票和风险计算规则

**Files:**
- Create: `src/EngineeringManager.Domain/Finance/FinanceEnums.cs`
- Create: `src/EngineeringManager.Domain/Finance/LedgerCalculator.cs`
- Create: `src/EngineeringManager.Domain/Finance/InvoiceAmountValidator.cs`
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `tests/EngineeringManager.Tests/Domain/LedgerCalculatorTests.cs`
- Create: `tests/EngineeringManager.Tests/Domain/InvoiceAmountValidatorTests.cs`

- [ ] 写测试，确认已收=收款-退款冲销、未收=应收-已收、已付=付款-付款冲销、未付=应付-已付-扣款。
- [ ] 写测试，确认超收/超付不阻止但标记风险，发票不含税+税额必须等于含税金额。
- [ ] 运行测试确认领域类型尚不存在而失败。
- [ ] 实现纯计算规则、财务枚举和财务权限键。
- [ ] 运行领域测试确认通过。

## Task 2：持久化财务单据、账户、发票和流水

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/FinancialAccount.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ReceivableEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/CollectionEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/RefundOrReversalEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PayableEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PaymentEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/PaymentReversalEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/DeductionEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/InvoiceEntry.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/InvoiceReceivableLink.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/InvoiceLineItemLink.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/AccountTransaction.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/AccountTransfer.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/FinanceModelTests.cs`

- [ ] 写 SQLite 测试，确认项目、合同、签约公司、合作单位、账户、收付款、发票和转账关系可以保存。
- [ ] 运行测试确认模型尚不存在而失败。
- [ ] 添加金额精度、唯一索引、并发标记和 Restrict 删除规则，避免财务历史被级联删除。
- [ ] 运行模型测试确认持久化通过。

## Task 3：财务录入和账户转账服务

**Files:**
- Create: `src/EngineeringManager.Application/Finance/FinanceDtos.cs`
- Create: `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Create: `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Create: `tests/EngineeringManager.Tests/Application/FinanceLedgerServiceTests.cs`

- [ ] 写测试，确认收款生成账户流入、付款生成账户流出、退款生成账户流出、内部转账同时生成转出和转入。
- [ ] 写测试，确认超收/超付允许保存并在汇总中返回风险标记。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现应收、收款、退款、应付、付款、扣款、付款冲销和账户转账事务服务。
- [ ] 运行服务测试确认业务单据与账户流水一致。

## Task 4：发票和项目经营汇总服务

**Files:**
- Modify: `src/EngineeringManager.Application/Finance/FinanceDtos.cs`
- Modify: `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Create: `tests/EngineeringManager.Tests/Application/FinanceSummaryTests.cs`

- [ ] 写测试，确认销项/进项发票可关联应收和多个清单项，并计算已开票/未开票。
- [ ] 写测试，确认项目汇总同时返回应收、已收、未收、应付、已付、未付和风险标记。
- [ ] 运行测试确认汇总行为尚未实现而失败。
- [ ] 实现发票录入、关联和项目/合同/签约公司/合作单位维度汇总查询。
- [ ] 运行汇总测试确认经营口径通过。

## Task 5：财务总览、账户页面和授权

**Files:**
- Modify: `src/EngineeringManager.Web/Program.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Finance/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Finance/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Finance/Accounts.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Finance/Accounts.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Finance/Entries/Create.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Web/FinanceAuthorizationTests.cs`

- [ ] 写 Web 测试，确认财务人员和管理员可进入录入页，查询人员只读查看总览，项目负责人和现场人员不能进入财务录入。
- [ ] 运行测试确认页面尚不存在而失败。
- [ ] 实现中文财务指标卡、账户列表和最小业务录入入口。
- [ ] 页面只调用财务服务，不直接计算余额或经营指标。
- [ ] 运行 Web 测试确认权限边界和页面响应通过。

## Task 6：迁移、质量门禁和进度更新

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_InternalFinanceLedger.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 运行完整质量门禁，确认 Release 构建 0 警告、全部测试通过。
- [ ] 创建并应用阶段 4 EF Migration 到本机 SQL Server。
- [ ] 真实启动 Web，验证财务页面未登录边界、健康检查和数据库就绪状态。
- [ ] 更新唯一进度文件，记录迁移名称、测试数量、经营口径、遗留风险和阶段 5 计划。

## 阶段 4 完成定义

- 合同金额与应收记录分离，允许进度款、质保金和人工经营应收。
- 收款、付款、退款、冲销、扣款和转账均有独立记录及账户流水。
- 销项/进项发票与应收、合同清单项保持多对多关联。
- 超收/超付不阻止保存，但汇总必须明确返回风险。
- 不实现会计科目、总账、资产负债表、利润表、税务申报和员工工资。
