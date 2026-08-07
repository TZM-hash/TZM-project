# 项目金额与清单项数量清理实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to execute this plan task-by-task with verification checkpoints.

**Goal:** 删除项目层预计金额、已结算金额和清单项数量的冗余契约与展示，只保留合同金额、当前工程金额和现有结算状态，同时保持工程量明细口径切换和自动计算不变。

**Architecture:** 保持 `ContractLineItem.Quantity/UnitPrice -> ProjectAmountCalculator.CurrentAmount` 数据流不变，收敛领域/应用层项目汇总 DTO，再让 Razor 项目列表和两个导出实现只消费剩余字段。删除仅发生在项目汇总投影和展示契约，不修改明细实体、数据库迁移或无关财务/设备汇总。

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, EF Core, xUnit, FluentAssertions, ClosedXML-compatible project workbook exporters.

---

## 受影响文件

- `src/EngineeringManager.Domain/Projects/ProjectAmountCalculator.cs`: 删除项目金额分桶结果，保留当前金额、发票口径和结算状态。
- `src/EngineeringManager.Application/Projects/ProjectSummaryDto.cs`: 删除项目汇总的预计金额、已结算金额、清单项数量。
- `src/EngineeringManager.Infrastructure/Projects/ProjectSummaryService.cs`: 按新 DTO 构造汇总。
- `src/EngineeringManager.Web/Pages/Projects/Index.cshtml` 与 `.cs`: 删除列表列、列管理选项和导出列选项，保留当前工程金额。
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`: 删除工程量明细标签的数量徽标，保留明细表和口径编辑。
- `src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`: 删除传统项目清单导出的三个字段及行映射。
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectListWorkbookExporter.cs`: 删除项目清单工作簿的三个列定义、布局、合计与行值。
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookCatalog.cs`: 删除项目经营汇总工作表的预计/已结算字段，保留当前工程金额。
- `src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`: 删除项目经营汇总工作表行值中的预计/已结算字段。
- `tests/EngineeringManager.Tests/Domain/ProjectAmountCalculatorTests.cs`: 改为验证统一当前金额跨阶段保持不变。
- `tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`、`ProjectWorkspaceServiceTests.cs`、`EndToEndBusinessFlowTests.cs`: 更新项目汇总契约断言，同时保留明细金额断言。
- `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`、`ProjectColumnLayoutTests.cs`: 验证项目页面不再注册或渲染删除字段。
- 相关数据交换测试：验证导出表头/字段目录不再包含删除字段，工程量表仍完整。

## Task 1: 先更新契约测试与领域结果

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Domain/ProjectAmountCalculatorTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectWorkspaceServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Integration/EndToEndBusinessFlowTests.cs`

- [ ] **Step 1: 将测试断言改为统一金额语义**

保留每个测试对 `CurrentAmount`、`InvoiceRequiredAmount` 和 `SettlementStatus` 的断言；删除 `ProjectAmountSummary.EstimatedAmount`/`SettledAmount` 以及 `ProjectSummaryDto.EstimatedAmount`/`SettledAmount` 的断言。为暂估阶段和部分结算阶段各保留一个断言，确认两者的 `CurrentAmount` 都等于明细金额。

- [ ] **Step 2: 运行定向测试并确认契约尚未实现**

Run from `EngineeringManager`:

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter 'FullyQualifiedName~ProjectAmountCalculatorTests|FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~ProjectWorkspaceServiceTests|FullyQualifiedName~EndToEndBusinessFlowTests' --no-restore
```

Expected: FAIL to compile because the production summary records still contain the old constructor/property contract. This is the expected red step before the implementation.

## Task 2: 收敛领域与应用项目汇总契约

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Projects/ProjectAmountCalculator.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectSummaryDto.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectSummaryService.cs`

- [ ] **Step 1: 让 `ProjectAmountSummary` 只返回剩余结果**

将结果记录调整为：

```csharp
public sealed record ProjectAmountSummary(
    decimal CurrentAmount,
    decimal InvoiceRequiredAmount,
    ProjectSettlementStatus SettlementStatus);
```

保留当前金额、应开票金额和阶段到结算状态的计算；删除把当前金额分配到 `estimatedAmount`/`settledAmount` 的局部变量与构造参数。

- [ ] **Step 2: 让 `ProjectSummaryDto` 只暴露项目层有效字段**

将记录调整为：

```csharp
public sealed record ProjectSummaryDto(
    decimal ContractAmount,
    decimal CurrentAmount,
    ProjectSettlementStatus SettlementStatus,
    int ContractCount,
    decimal InvoiceRequiredAmount = 0m);
```

`LineItemCount` 不再作为汇总契约返回；工程量明细仍通过各合同的 `LineItems` 返回。

- [ ] **Step 3: 更新汇总服务并运行 Task 1 测试**

让 `ProjectSummaryService.Calculate` 传入合同金额、`amountSummary.CurrentAmount`、`SettlementStatus`、合同数量和 `InvoiceRequiredAmount`。运行 Task 1 的定向测试，预期全部通过。

## Task 3: 清理项目管理页面与列状态

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectColumnLayoutTests.cs` when a page-source assertion is needed.

- [ ] **Step 1: 删除项目列表的三个字段**

从 `ProjectExportColumnDefinitions`、`BuildWorkbench` 的列列表、`Index.cshtml` 表头和数据行同时删除 `estimated_amount`、`settled_amount`、`line_item_count`。保留 `contract_amount`、`current_project_amount`、`settlement_status` 和 `contract_count`，不修改当前金额排序和筛选键。

- [ ] **Step 2: 删除详情页的工程量数量徽标**

将工程量标签从：

```cshtml
<label for="project-tab-quantity">工程量明细 <span>...</span></label>
```

改为只显示 `工程量明细`，不改其他收款、开票、付款和施工详情标签数量，也不改工程量表的实际行内容。

- [ ] **Step 3: 更新 Web 回归断言**

将项目列表测试从断言旧列存在改为断言 `contract_amount`、`current_project_amount` 存在且三个旧 key/中文标题不存在；增加详情页源码不包含工程量标签数量徽标的断言。运行 Web 相关测试。

## Task 4: 清理两套项目导出契约

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectListWorkbookExporter.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookCatalog.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ProjectWorkbookExporter.cs`
- Modify: affected files under `EngineeringManager/tests/EngineeringManager.Tests/Application` for export catalog/header assertions.

- [ ] **Step 1: 清理传统项目清单导出**

从 `ExportDataset.ProjectOverview` 的字段定义和项目行字典中删除 `estimated_amount`、`settled_amount`、`line_item_count`。保留 `current_project_amount` 与 `contract_count`。

- [ ] **Step 2: 清理项目清单工作簿导出**

同步删除三个 key 的列定义、列宽布局、`TotalColumns` 合计集合和 `RenderRow` 字典值；不能删除工程量工作表的 `quantity`、`unit_price`、`accounting_label` 或其他工程量字段。

- [ ] **Step 3: 清理项目经营汇总工作簿**

从 `ProjectWorkbookCatalog` 的 `ProjectSummary` 字段目录和 `ProjectWorkbookExporter` 的项目行值删除预计/已结算字段，保留合同金额、当前工程金额及收付款/开票字段。更新目录与工作簿测试，确认字段不存在且工作表仍可生成。

## Task 5: 全量验证与残留引用审计

- [ ] **Step 1: 运行项目定向测试**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --no-restore
```

Expected: PASS.

- [ ] **Step 2: 构建与质量门禁**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 build .\EngineeringManager.sln --configuration Release --no-restore
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\quality-gate.ps1
```

Expected: build and quality gate exit successfully.

- [ ] **Step 3: 审计项目层残留引用**

```powershell
$ErrorActionPreference = 'Stop'
rg -n --glob '!**/bin/**' --glob '!**/obj/**' 'Summary\.(EstimatedAmount|SettledAmount|LineItemCount)|ProjectSummaryDto\(|ProjectAmountSummary\(|estimated_amount|settled_amount|line_item_count|清单项数量' src tests
```

Expected: no project-list/project-summary/page/export references remain; any remaining `ContractLineItemDto` source compatibility fields or unrelated equipment/company fields must be reviewed against the design boundary before completion.

- [ ] **Step 4: Inspect final diff and preserve unrelated changes**

Run `git status --short` and `git diff --check`; verify only this task's files changed and no migration or generated binary was added.
