# 全系统列表排序盘点

## 范围结论

- Razor 页面中共发现 74 个 `<table>`，分布在 44 个含表格或集合循环的页面/局部视图中。
- 所有可见 `.data-table` 默认纳入排序增强；隐藏的图表辅助表、空状态占位表不显示排序菜单。
- 普通表单下拉选项不是“列表页面”，不额外嵌套第二个排序控件；其业务主档选项仍应优先显示较新的业务编号。
- 合计行、空状态行和明确标记为固定的行不参与重排。

## 服务端排序后分页

| 页面 | 主列表 | 默认键 |
| --- | --- | --- |
| `Pages/Projects/Index.cshtml` | 项目总览 | `ProjectNumber DESC` |
| `Pages/Employees/Index.cshtml` | 员工主档 | `EmployeeNumber DESC` |
| `Pages/Finance/Index.cshtml` | 项目经营台账（兼容页） | `ProjectNumber DESC` |
| `Pages/Ledger/External/Index.cshtml` | 外部中央账本结算 | `BusinessDate DESC` |
| `Pages/Ledger/Internal/Index.cshtml` | 内部中央账本结算 | `BusinessDate DESC` |

这些页面必须在分页前完成排序，不能只调整当前页 DOM。

## DataWorkbench 客户端全量排序

- `Pages/Admin/Organizations.cshtml`
- `Pages/Admin/Users.cshtml`
- `Pages/Backups/Index.cshtml`
- `Pages/Companies/Certificates/Index.cshtml`
- `Pages/Companies/Index.cshtml`
- `Pages/Crews/Index.cshtml`
- `Pages/Employees/Certificates/Index.cshtml`
- `Pages/Employees/Ledger.cshtml`
- `Pages/Equipment/Index.cshtml`
- `Pages/Partners/Index.cshtml`
- `Pages/Payroll/Index.cshtml`
- `Pages/Reminders/Index.cshtml`
- `Pages/StageResults/Index.cshtml`

这些页面当前把完整筛选结果渲染到浏览器，排序菜单可以重排全部已加载数据，并按页面与表格保存用户选择。

## 小页面、详情页、弹窗与局部表格

共享脚本自动增强以下页面中的可见 `.data-table`：

- `Pages/Index.cshtml` 的项目现金观察表。
- `Pages/Admin/BusinessYears/Index.cshtml`、`Pages/Admin/FinanceYears/Index.cshtml`。
- `Pages/Backups/Index.cshtml` 的计划与历史子表。
- `Pages/Companies/Details.cshtml` 的归属、证书、账户及关联业务子表。
- `Pages/Crews/Details.cshtml` 与 `Pages/Crews/Index.cshtml` 的成员、往来和名册表。
- `Pages/DataExchange/Index.cshtml` 的导出字段、导入预览、任务历史与错误明细。
- `Pages/Employees/Details.cshtml` 的归属、证书、工资、报销和往来子表。
- `Pages/Equipment/Index.cshtml` 的使用历史表。
- `Pages/Finance/Accounts.cshtml`。
- `Pages/Ledger/External/Index.cshtml`、`Pages/Ledger/Internal/Index.cshtml` 的收付款、发票、扣款、工资和审计子表。
- `Pages/Ledger/Reconciliations/Index.cshtml`、`Pages/Ledger/Reconciliations/Details.cshtml`、`Pages/Ledger/Years/Index.cshtml`。
- `Pages/Partners/Details.cshtml`。
- `Pages/Payroll/_PayrollEditor.cshtml` 与 `Pages/Payroll/Index.cshtml` 的工资行、收款人名册。
- `Pages/Projects/Details.cshtml` 的工程量、收款、发票、应付、付款和施工记录。

## 默认“最新”识别顺序

1. 页面/表格显式提供的业务排序键。
2. 日期或时间列，降序；空值始终放最后。
3. 项目、员工、合作单位、公司、设备等业务编号列，使用中文环境自然数字比较后降序。
4. 其他列表保留服务端原始业务顺序作为“最新在前”，并允许切换“最早在前”或按具体列排序。

## 排序菜单选项

- 固定提供“最新在前”“最早在前”。
- 按可见列生成升序/降序选项，跳过操作、附件、选择框等非业务列。
- 金额与百分比按数值比较，日期按日期比较，业务编号按自然数字比较，其他内容按中文文本比较。
- 工程量、里程碑等具有业务语义的表格保留“原业务顺序”选项。

## 验证范围与剩余风险

- 静态覆盖测试扫描全部可见 `.data-table`，并排除 `sr-only` 图表辅助表和显式禁用表。
- 回归测试覆盖 DataWorkbench 与独立表格、空表/单行表、动态行删除、自然数字、日期、金额、百分比、动态插入、固定行、本地记忆，以及服务端 URL 参数的大小写兼容、第一页复位和保存视图退出。
- 项目、员工、财务兼容页和两个中央账本的服务端排序均受分页前排序测试或页面契约测试保护。
- 真实数据库只读查询确认 `ProjectNumber DESC` 首条是 `XM0227`。
- 剩余风险仅为受登录拦截的浏览器端视觉与交互复核：排序菜单切换、刷新保持、各详情/弹窗页面以及桌面与移动宽度布局仍需在用户登录后抽查。
