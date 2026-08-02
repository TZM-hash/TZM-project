# 项目总览与短编号修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复全项目财务总览缺失并把历史自动编号、自动占位名称安全缩短。

**Architecture:** 在统一财务汇总服务中补入未过账工程量，不在页面重复业务计算；通过幂等事务服务和一次性 CLI 命令修复现有业务编号。页面只负责完整、中文、无重叠地呈现统一 DTO。

**Tech Stack:** .NET 9、ASP.NET Core Razor Pages、EF Core、SQL Server、SQLite/xUnit、Python unittest/openpyxl。

---

### Task 1: 建立财务汇总回归测试

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/FinanceSummaryTests.cs`

- [x] 添加测试：仅有两条工程量、没有 `FinanceSettlement` 时，应收为 100、应开为 100、未收和未开均为 100。
- [x] 添加测试：其中一条工程量已有活动 `ProjectQuantity` 结算时，总应收仍为 100，不能重复为 160。
- [x] 扩展直接项目收款/发票测试，断言直接收款冲减未收、直接发票冲减未开。
- [x] 运行：`dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~FinanceSummaryTests`。
- [x] 确认测试因当前汇总遗漏工程量而按预期失败，而不是编译或测试装配错误。

### Task 2: 实现未过账工程量汇总兜底

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`

- [x] 在 `BuildProjectSummariesAsync` 中查询全部活动工程量结算来源 ID。
- [x] 在无截止日期时读取匹配项目和筛选维度的活动合同工程量，排除已有活动来源 ID。
- [x] 将每条兜底工程量转换为 `CentralLedgerMetrics` 并加入应收累计器，发票基数取决于 `RequiresInvoice`。
- [x] 修改累计器，使直接销项发票从未开金额中扣除并按零下限截断。
- [x] 重新运行 Task 1 定向测试并确认全部通过。

### Task 3: 建立短编号修复回归测试

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/LegacyDataRepairServiceTests.cs`

- [x] 在 SQLite 测试库中建立旧项目、合同、工程量、员工、合作单位、发票和占位公司，同时预置 `XM0001`、`YG0001` 等碰撞编号。
- [x] 断言修复后格式正确、自动顺延、同一合同工程量从 `QD001` 起编号、实体 ID 和关联关系不变。
- [x] 断言自动合同、工程量、合作单位、签约公司和财务账户名称被缩短，且第二次执行不重复修改。
- [x] 第二次调用修复服务并断言所有变更计数为零。
- [x] 运行该测试并确认因服务尚不存在而按预期失败。

### Task 4: 实现幂等短编号修复服务和 CLI

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/LegacyDataRepairService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`

- [x] 定义修复结果与映射记录，加载目标实体并生成碰撞安全的新编号。
- [x] 识别已知自动占位名称并改为简短中文名称，来源旧编号只保留在映射报告或备注中。
- [x] 在单事务中执行临时编号阶段和正式编号阶段，失败时整体回滚。
- [x] 注册服务并增加 `--repair-legacy-project-data` 命令。
- [x] 命令先写 SQL Server `.bak`，再执行修复并输出 JSON 映射报告。
- [x] 运行 Task 3 测试并确认通过。

### Task 5: 更新未来自动编号规则

**Files:**
- Modify: `EngineeringManager/scripts/convert_legacy_project_data.py`
- Modify: `EngineeringManager/scripts/tests/test_convert_legacy_project_data.py`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/EmployeeWorkbookImporter.cs`
- Modify: affected C# test if existing assertion expects unpadded `YG1`

- [x] 先把 Python 断言改为 `XM0001` 并运行确认失败。
- [x] 旧项目转换按项目稳定顺序生成 `XM0001` 起的短编号，并更新导入说明。
- [x] 工程量源名称为空时生成 `待确认工程量N`，不得把旧编号拼入展示名称。
- [x] 员工工作簿自动补号统一为 `YG0001` 格式并保持碰撞跳号。
- [x] 运行：`python -m unittest EngineeringManager/scripts/tests/test_convert_legacy_project_data.py`。
- [x] 运行相关员工工作簿测试。

### Task 6: 修复项目详情金额、中文和表格显示

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Presentation/ProjectDisplayText.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ChineseDisplayTests.cs`

- [x] 先添加静态测试，要求页面调用日期和支付方式中文格式化，并要求项目财务表允许单元格内换行。
- [x] 顶部经营条与金额进度补齐三组应收/开票/应付金额及比例。
- [x] `DateOnly.MinValue` 显示“日期待确认”，支付方式显示中文。
- [x] 覆盖项目明细表的全局不换行规则，保留日期和金额列不换行。
- [x] 运行 Web 定向测试并确认通过。

### Task 7: 执行真实数据库修复和全库审计

**Files:**
- Generated outside git: `EngineeringManager/src/EngineeringManager.Web/App_Data/backups/*.bak`
- Generated outside git: `EngineeringManager/src/EngineeringManager.Web/App_Data/logs/legacy-data-repair-*.json`

- [x] 停止旧服务进程，确认数据库无正在进行的写操作。
- [x] 运行 Release 构建后的 `--repair-legacy-project-data` 命令；记录备份和映射报告路径。
- [x] 再运行一次命令验证零变更幂等性。
- [x] 用 SQL 审计旧前缀数量、唯一键重复、实体数量和孤立关系。
- [x] 动态审计所有名称/编号/编码字段，确认没有遗留旧编号、GUID 式展示值或已知自动长名称。
- [x] 审计所有项目：工程量应收缺失数量归零（以汇总兜底口径）、截图项目应收/已收/未收符合验收值。

### Task 8: 修复员工工作簿结构标签误导入

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/EmployeeWorkbookImporter.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/StandardImportTests.cs`

- [x] 用原始工作簿定位“已付款”位于个人明细页姓名列下方的分区标题，并确认空白模板保留 0 金额占位行。
- [x] 先新增失败测试，复现空白模板生成“已付款”伪员工及非零无归属明细被错误接收。
- [x] 参与人解析跳过分区标题，应付与已付同时为 0 的明细不进入分析结果。
- [x] 定向测试通过；确认伪员工所有外键关联为 0 后从测试库安全删除。
- [x] 全库员工数恢复为 96，员工编号均符合 `YG0001` 格式且不存在结构标签姓名。

### Task 9: 完整验证、服务复核和提交

**Files:**
- Modify if warranted: `.trellis/spec/backend/database-guidelines.md`
- Modify if warranted: `.trellis/spec/frontend/component-guidelines.md`

- [x] 浏览器发现开票明细仍显示 `Output`，先补失败测试，再由 `ProjectDisplayText` 映射为“销项发票/进项发票”。
- [x] 新增 `ShortBusinessNumber`，项目、员工、合作单位和施工班组的新建/复制入口建议下一个标准短编号；项目查询包含停用项目。
- [x] 清除所有生产代码中的 `-COPY`、“ - 副本”和“（复制）”自动值，公司、设备复制名称统一为“（副本）”。
- [x] 运行短编号、中文显示、合作单位、班组、员工、公司、设备相关定向测试，67/67 通过。
- [x] 运行全部测试：`dotnet test EngineeringManager/EngineeringManager.sln`。
- [x] 运行 Release 构建：`dotnet build EngineeringManager/EngineeringManager.sln -c Release --no-restore`。
- [x] 启动 Release 服务并检查 `/health/live` 与 `/health/ready`，均返回 200。
- [x] 打开截图项目和若干有/无结算项目，核对金额、比例、短编号、中文日期和表格布局。
- [x] 检查 `git diff`，当前工作区无未提交改动。
- [x] 按仓库提交风格提交本任务文件和代码，并推送当前分支。

## 当前验证记录

- 核心回归测试通过：146/146（财务汇总、工作台、历史数据修复、员工完整工作簿、短编号、中文显示、复制入口和相关页面静态测试）。
- Release 构建通过：0 个警告、0 个错误。
- 完整测试程序集执行到 875 项，其中 780 项通过；剩余失败均集中在当前沙盒访问 SQL Server `localhost\\SQLEXPRESS` 的连接/SSPI 环境，以及不经 Web 宿主的旧 DPAPI 测试密钥目录权限，未发现新的业务断言失败。
- 真实数据库修复尚未执行：维护命令在 SQL Server 备份前因 SSPI 认证失败退出，因此没有生成备份、映射报告或数据库变更。

## 2026-08-02 续验记录

- 完整测试重新执行通过：875/875，无失败。
- 当前 Release 服务已在 `http://127.0.0.1:5075` 运行；`/health/live` 与 `/health/ready` 均返回 HTTP 200 `Healthy`。
- 最新一次真实修复报告为 `legacy-data-repair-20260801210610_f66b3f801a3841758f1db0ccdb02f523.json`，备份路径为同名 `EngineeringManager_LegacyRepair_*.bak`；再次执行结果 `TotalChanges=0`。
- SQL Server 只读审计确认：227 个项目、227 个合同、737 条工程量、96 名员工、462 个合作单位、482 张发票、9 个签约公司均无旧前缀；占位长名称、重复编号和项目/合同/工程量/财务关联孤立记录均为 0。
- `XM0007` 当前工程量应收基数为 `1,211,576.00`，已收 `1,105,550.00`，未收 `106,026.00`，与验收值一致。
- 页面详情复核尚未完成：当前测试库仅保留 `codex-qa-admin` 与 `taozhiming`，仓库中可找到的旧演示密码已失效；未猜测密码，也未擅自重置账号。

## 2026-08-02 续验记录（提醒与动态修复）

- 新增 `ProjectDisplayText.ActivityDateLabel(DateTimeOffset)`，未知工程业务日期在项目动态中显示“日期待确认”，不再显示 `01-01 08:00`。
- `DashboardService` 对系统生成的项目提醒按当前项目编号兜底重建显示文本；`LegacyDataRepairService` 在同一事务中同步持久化项目提醒消息。
- 新增日期显示、Dashboard 旧提醒编号和历史修复提醒消息回归测试；定向测试 19/19 通过。
- 完整测试重新执行通过：876/876，无失败。
- Release 构建重新执行通过：0 个警告、0 个错误；旧服务进程已停止后完成构建并重新启动。
- 新增真实修复报告：`legacy-data-repair-20260802010302_62c92fe7a89f44bcaa9029e1d06e186b.json`，对应备份 `EngineeringManager_LegacyRepair_20260802010302_62c92fe7a89f44bcaa9029e1d06e186b.bak`，同步 65 条提醒消息；再次执行报告 `legacy-data-repair-20260802010316_7127e39c5b574180bfce5ea70652c5a4.json`，`LEGACY_DATA_REPAIR_CHANGES=0`。
- Release 服务 `http://127.0.0.1:5075` 的 `/health/live` 与 `/health/ready` 均返回 HTTP 200 `Healthy`。
- 管理员 `taozhiming` 浏览器复核：首页经营风险提醒全部使用 `XM####`；XM0005 收款记录显示“日期待确认 / 银行转账”，右侧动态显示“收款记录 · 日期待确认”，应收/已收/未收为 `1,565,370.00 / 800,000.00 / 765,370.00`。
- SQL Server 只读复核：提醒标题/消息及项目、合同、工程量、员工、合作单位、发票、签约公司编号中的 `OLD-*` / `OFFICIAL-*` 均为 0。

## 2026-08-02 续验记录（中央账本统一化）

- 财务导入按单头与分摊行分组后统一委托 `CentralLedgerCommandService` 校验和写入；来源模块生成的设备结算、工资付款记录仍只能从来源模块修改或冲销。
- 公司财务列表、项目工作簿导入导出、中央账本查询与项目详情统一支持多项目分摊，并保留旧财务链接的兼容回退。
- 设备结算和工资班组分摊新增中央应付链接迁移 `20260802005033_CentralFinanceLinksForEquipmentAndPayroll`；本轮只读查询确认测试库尚无两个 `FinanceSettlementId` 列，未擅自应用迁移。
- 公共发票创建拒绝未定义的 `LedgerRecordStatus`，发票编号唯一性同时检查数据库和当前 `DbContext` 中尚未保存的本地实体。
- 资金冲销可同时处理原记录的已分摊金额和单头未分摊余额，并扣除既有有效冲销，禁止超过剩余可冲销额度。
- 新增回归测试 `CreatingInvoiceRejectsUndefinedStatus` 与 `RefundCanReverseAllocatedAndUnallocatedCollectionAmounts`，均完成先失败后修复通过；中央账本分摊测试 8/8、扩大财务测试组 210/210 通过。
- 完整测试重新执行通过：909/909；Release 构建通过：0 个警告、0 个错误。
- Release 服务运行于 `http://127.0.0.1:5075`，`/health/live` 与 `/health/ready` 均返回 HTTP 200 `Healthy`。

### Task 10: 建立全系统排序 UI 失败测试

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/ListSortingAssetTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs`

- [x] 新增断言：DataWorkbench 必须显示 `data-list-sort-menu` 和“排序”标签。
- [x] 新增断言：站点在存在 `.data-table` 时加载 `list-sorting.js`。
- [x] 新增断言：共享脚本必须覆盖独立表格、中文自然数字、日期/金额解析、本地记忆、服务端 URL 参数和动态表格。
- [x] 新增扫描断言：所有可见 `.data-table` 都由共享选择器覆盖，`sr-only` 图表表格被排除。
- [x] 运行 `dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ListSortingAssetTests|FullyQualifiedName~DataWorkbenchAssetTests`，确认因功能尚不存在而失败。

### Task 11: 实现共享排序菜单和小列表自动增强

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchViewModel.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/list-sorting.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/site.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`

- [x] 为 DataWorkbench 增加排序选项、默认键、默认方向和服务端/客户端模式。
- [x] 在共享工具栏渲染排序选择器，并统一使用 `sortKey` / `sortDescending` 参数名。
- [x] 实现独立表格自动菜单、稳定比较、日期/数值/中文短编号解析、固定行处理和本地保存。
- [x] 用 `MutationObserver` 处理动态弹窗表格，并阻止脚本重复插入菜单或监听器。
- [x] 重新运行 Task 10 测试并确认通过。

### Task 12: 项目与员工在分页前默认最新排序

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectServiceTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/EmployeeListSortingTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Employees/Index.cshtml`

- [x] 先写项目与员工失败测试：默认项目编号/员工编号降序，且排序发生在分页前。
- [x] 项目默认 `SortDescending=true`，提供编号、名称、阶段、金额和结算状态选项。
- [x] 员工增加 `SortKey` / `SortDescending`，在 `Skip/Take` 前按编号、姓名、类型、金额或状态排序。
- [x] 员工筛选链接、保存返回和分页链接保留排序参数。
- [x] 定向测试通过，并确认 `XM0227` 的排序契约受测试保护。

### Task 13: 中央账本主表默认业务日期降序

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/CentralLedgerQueryServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/CentralLedgerPageTests.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Finance/CentralLedgerDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/External/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Ledger/Internal/Index.cshtml`

- [x] 先写失败测试：未传排序参数时，较新的业务日期必须位于第一行。
- [x] 两个账本 PageModel 绑定并传递排序键/方向，默认 `BusinessDate DESC`。
- [x] 主表标记为服务端排序，菜单提供日期、金额、项目和往来单位选项；子表继续由共享客户端排序覆盖。
- [x] 页签、重置与筛选 URL 保留排序参数。
- [x] 定向测试通过。

### Task 14: 全量验证与真实页面复核

**Files:**
- Modify if regression requires: affected Razor page or service query only.

- [x] 运行排序相关 Web、项目、员工、中央账本定向测试。
- [x] 运行 `dotnet test EngineeringManager/EngineeringManager.sln`。
- [x] 运行 `dotnet build EngineeringManager/EngineeringManager.sln -c Release --no-restore`。
- [x] 重启 `http://127.0.0.1:5075` 服务并检查 `/health/live`、`/health/ready`。
- [ ] 浏览器验证项目首条 `XM0227`，切换编号/名称排序并刷新。
- [ ] 抽查员工、中央账本、项目详情、公司详情、工资弹窗和数据导入小表的排序菜单与交互。
- [ ] 检查控制台错误、桌面与移动宽度布局、表格横向滚动和排序菜单不遮挡。
- [x] 对照 `research/list-sorting-inventory.md` 完成覆盖复核并记录剩余风险。

## 2026-08-02 续验记录（全系统列表统一排序）

- 项目、员工、项目经营台账、外部中央账本和内部中央账本均在分页前执行服务端排序；默认分别使用业务编号或业务日期降序。
- DataWorkbench、详情页、弹窗和其他小表由共享 `list-sorting.js` 提供“最新在前 / 最早在前”及按列排序，并按 URL 或浏览器本地状态保存选择。
- 修复无实际表格或不足两条业务行的工作台仍显示排序菜单的问题；独立动态表格从多行缩减到零至一行时也会自动隐藏菜单。相关回归测试均完成先失败后最小修复通过。
- 服务端排序、页码与保存视图参数现在按大小写不敏感方式清除旧值，再统一写入 `sortKey` / `sortDescending`；切换排序会回到第一页并退出旧保存视图，避免旧值覆盖新选择。
- 排序定向回归通过：46/46；四个相关 JavaScript 模块的 `node --check` 和 URL 清理行为检查通过；完整测试通过：918/918。
- Release 构建通过：0 个警告、0 个错误；服务已重新启动为 PID 27756，`/health/live` 与 `/health/ready` 均返回 HTTP 200 `Healthy`。
- SQL Server 只读查询确认按 `ProjectNumber DESC` 的首条为 `XM0227 | （宏达）临政工出【2026】2号年产5000吨紧固件生产线项目`，本轮未执行数据库写入。
- 内置浏览器确认登录页可加载、非空白、无控制台警告或错误，密码显示切换可正常交互；当前保存的登录信息被系统拒绝，未猜测或重置密码，因此受保护列表的真实页面排序、刷新记忆和响应式布局复核仍待用户登录后完成。
