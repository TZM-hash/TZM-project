# 系统设置与数据导入导出实施计划

> 按测试先行执行；每个小项先新增一个能体现需求的失败测试，确认失败原因正确后再修改生产代码。

## 1. 激活任务与上下文

- [x] 更新 `implement.jsonl`、`check.jsonl` 为实际规范上下文。
- [x] 运行 `python ./.trellis/scripts/task.py validate system-settings-data-io`。
- [x] 运行 `python ./.trellis/scripts/task.py start system-settings-data-io`，确认状态为 `in_progress`。

## 2. 全局字号

文件：`Application/Settings/SystemSettingsDtos.cs`、`Infrastructure/Settings/SystemSettingsService.cs`、`Web/Pages/Admin/Settings/Index.cshtml(.cs)`、`Web/Pages/Shared/_Layout.cshtml`、`Web/wwwroot/css/base.css`、`Web/wwwroot/js/pages/settings.js`；测试：`Application/SystemSettingsServiceTests.cs`、`Web/SystemSettingsPageTests.cs`。

- [x] 新增默认标准、非法值回退、保存和缓存失效的失败测试。
- [x] 新增设置 DTO/规范化逻辑和持久化字段；在布局输出 `<html class="font-size-standard">` 类。
- [x] 新增四档 CSS 根变量及设置页选项/预览，确保无设置数据时仍保持 15px。
- [x] 运行设置应用和 Web 测试，确认默认与持久化行为。

## 3. 页面切换与财务汇总

文件：`Web/wwwroot/js/site.js`、`Web/wwwroot/css/themes.css` 或页面动效样式、`Infrastructure/Finance/FinanceLedgerService.cs`、必要的 DTO/查询服务；测试：`Application/FinanceSummaryTests.cs`、`Performance/RepresentativeDataPerformanceTests.cs`、Web 页面测试。

- [x] 新增无分摊项目级现金/发票仍进入总览及比率计算的失败测试；新增批量汇总查询不逐项目调用重型明细的失败测试/查询计数断言。
- [x] 用批量聚合替换项目列表中的 N+1，保留详情接口的完整明细语义，分母为零返回 0。
- [x] 让现金/发票直接项目关联和分摊关联统一进入项目桶，按记录 ID 去重。
- [x] 新增导航 pending 状态测试；把 `site.js` 的非关键模块从顶层阻塞 Promise 中移入空闲任务，确保关键初始化先返回；缩短页面进入动画。
- [x] 运行财务和代表性性能测试，记录 SQL/耗时结果。

## 4. 员工 Excel 工资与往来

文件：现有 `.tmp-formal-import/EmployeeSupplementalImport.cs` 或抽取为正式数据交换组件、`Infrastructure/EmployeeAnnualLedger/EmployeeAnnualLedgerService.cs`、相关工资实体/服务、应用测试；源文件：`old-data/员工相关信息2026年.xlsx`。

- [x] 为 2026 业务年度、Excel 公式结果、幂等业务键和未付计算新增失败测试/固定样例测试。
- [x] 实现工作簿读取、姓名/员工号匹配、金额规范化和来源行记录；按预览/正式模式输出错误报告。
- [x] 导入工资明细、已发/未发和员工往来，重复运行只更新同一来源业务键，不追加重复流水。
- [x] 在测试库创建/复用 2026 年度，正式执行补导；用 SQL 和报告核对总额、随机员工公式结果和重复执行数量。

## 5. 中文化与短编号

文件：全局枚举显示映射、Razor 页面、`DataExchange/ImportService.cs`、`DataExchange/ExportService.cs`、编号生成位置及测试。

- [x] 为现有面向用户的英文枚举/状态新增失败测试，确认输出必须为中文。
- [x] 集中实现中文显示和中英文兼容解析，替换导出及关键页面的 `.ToString()` 直出。
- [x] 为新建业务编号新增长度/唯一性测试；只替换新记录生成器，保留历史编号查询。
- [x] 扫描 UI、模板和导出结果中的英文残留，修正直接面向用户的字符串。

## 6. 导入导出模板

文件：`Infrastructure/DataExchange/ImportService.cs`、`ExportService.cs`、模板 DTO/测试及现有导入导出页面；测试：`Application/StandardImportTests.cs` 和新增中央账本/工资回读测试。

- [x] 先新增中央账本、工资、员工往来模板列和回读失败测试。
- [x] 抽取共用列定义/中文枚举解析，补齐财务收款、付款、发票、工资、工资批次、员工往来和账户导入导出。
- [x] 发票导出改读中央账本表；收款/付款导出包含直接项目关联和分摊；错误报告按行返回。
- [x] 生成并检查所有模板，执行导出→导入回读测试及旧格式兼容测试。

## 7. 综合验证与交付

- [x] 运行定向 xUnit/Web/性能测试并修复失败。
- [x] 使用 `dotnet test` 完整验证；使用 `dotnet build --no-restore /p:UseSharedCompilation=false` 构建。
- [x] 运行测试库校验脚本，输出员工、年度、项目财务和导入幂等统计。
- [x] 复查 `git diff`，确认不包含既有未提交文件的无关改动；更新任务状态和会话记录。
