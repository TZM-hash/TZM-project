# 工资台账筛选与汇总联动设计

## Scope

改动限定在工资台账 Razor Page、对应页面样式和页面模型测试。筛选在页面模型内基于工资批次现有字段完成，不新增迁移，不修改 `IPayrollService` 或公共 DTO。

## Filter Contract

页面新增可空查询参数 `DisbursementScope`，使用稳定字符串值：

- 空值：全部发放主体。
- `company:<legal-entity-guid>`：指定公司账户发放。
- `personal`：私人转账（现有 `PayrollFundingSource.PersonalAdvance`）。

页面模型负责解析该值。无效值按全部处理，避免手工修改 URL 产生异常。公司选项从启用的 `LegalEntity` 读取并按现有编码顺序显示。

## Data Flow

1. 工资服务按搜索词返回基础 `PayrollDisbursementOverviewDto`。
2. 页面模型依次应用发放主体和状态筛选，得到唯一的 `Batches` 结果集。
3. 页面模型用 `Batches` 重建页面级 `Overview`，四项汇总和状态计数全部从该结果集计算。
4. Razor 页面列表直接遍历 `Batches`，左侧汇总直接读取重建后的 `Overview`。
5. 状态快捷链接保留 `Search` 和 `DisbursementScope`；顶部表单通过 `_DataWorkbench` 统一提交三个条件。
6. 页面模型针对最终 `Batches` 的 ID，一次性查询 `PayrollPayments`，构建页面级 `RecipientBreakdowns` 字典。
7. 列表人数分项和详情名单都读取同一份 `RecipientBreakdowns`，不重复调用工资服务。

这种顺序保证筛选条件不会只作用于列表，也避免重复维护两套统计口径。

## UI Alignment

- 保留项目既有两栏工作台结构，但使工资页面复用设备、班组和合作单位页面的工作区边界、工具栏、汇总行和响应式约束。
- 移除工资页面对左右区域造成独立大卡片观感的专用背景、阴影或过高最小高度。
- 不新增嵌套卡片；左侧指标使用现有紧凑指标网格，右侧由集成工具栏和表格组成。
- 下拉框继续使用 `_DataWorkbench` 的 Select 字段，保持与其他页面一致的表单行为和控件尺寸。
- 日期列允许账户名称在单元格内换行，人数列使用两行紧凑统计，避免相邻列内容覆盖。
- 工资工具栏允许列管理菜单向下溢出显示；表格自身继续在 `payroll-table-wrap` 内横向滚动。
- 一级详情弹窗只显示批次摘要；“查看详细名单”打开独立二级 `dialog`，按员工和班组两区渲染名单。

## Recipient Breakdown

新增页面专用记录类型，不修改公共应用层 DTO：

- `PayrollRecipientItemViewModel`：姓名、班组名称、实际金额。
- `PayrollRecipientBreakdownViewModel`：员工集合、班组人员集合及派生人数。

姓名优先使用付款时保存的 `RecipientNameSnapshot`，为空时回退到当前员工/施工人员名称，再回退到收款人名称。班组名称优先使用 `CrewNameSnapshot`，为空时回退到当前班组名称。页面不展示身份证、银行卡或电话等敏感字段。

名单随当前列表一次性加载，并作为只读 JSON 放入对应批次的详情触发数据中。前端仅创建文本节点，不拼接不可信 HTML。

## Compatibility And Risk

- 旧 URL 没有 `DisbursementScope` 时行为保持为全部发放主体。
- 私人转账批次可能仍带有业务归属公司；选择公司时明确只统计 `CompanyAccount`，避免同一批次同时落入公司和私人转账选项。
- 编辑弹窗及保存逻辑不改动，但所有返回链接必须传递新查询参数。
- 主要回归风险是汇总与列表口径分离，通过页面模型测试覆盖组合筛选和计数。
- 名单查询必须限制在当前筛选后的批次 ID，避免全表读取；无批次时跳过查询。
- 二级弹窗关闭后一级详情弹窗保持打开，用户可以继续查看批次摘要。

## Validation

- 页面模型测试：公司账户、个人垫付、状态和搜索组合下的列表与汇总一致。
- Razor 资产测试：新增筛选字段、参数保留和页面结构存在。
- 页面模型测试：员工/班组人数和名单按批次正确分组。
- 前端资产测试：二级弹窗入口、名单渲染标记和列管理溢出修复存在。
- 定向测试：工资台账页面模型与页面资产测试。
- `Release` 构建。
- 桌面浏览器实际筛选和布局检查。
