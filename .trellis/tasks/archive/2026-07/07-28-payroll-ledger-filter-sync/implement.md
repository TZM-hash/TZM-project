# 工资台账筛选与汇总联动实施计划

## 1. 建立筛选行为测试

- 在工资台账页面模型测试中加入公司账户批次、个人垫付批次及不同状态批次。
- 断言 `company:<id>`、`personal`、状态和搜索组合均产生正确列表。
- 断言 `Overview` 的批次数、实际发放、员工发放、班组发放和状态计数来自同一筛选结果。

## 2. 实现页面模型筛选

- 为 `IndexModel` 增加 `DisbursementScope` 查询参数和发放主体选项。
- 在 `LoadWorkspaceAsync` 中解析发放主体并和状态条件叠加。
- 由最终 `Batches` 重建页面级 `Overview`。
- 保持现有项目、公司、账户标签查询，并只为页面需要的批次加载标签。

## 3. 更新工资台账视图

- 在 `_DataWorkbench` 内联筛选中加入“发放主体”下拉框。
- 根据当前筛选生成标题文案。
- 所有状态链接、新建和编辑链接保留 `DisbursementScope`。
- 调整左右工作区结构类名或局部标记，使其与现有设备/班组工作台模式一致。

## 4. 统一页面样式

- 删除或覆盖造成独立大卡片观感的工资专用背景、阴影和固定高度。
- 复用现有工作台的边界、间距、表格和紧凑汇总样式。
- 检查常见桌面宽度下筛选栏、表格和左侧汇总不重叠。

## 5. 验证

在验证前完成以下新增实现：

- 为工资页面增加页面级人员明细视图模型。
- 在 `LoadWorkspaceAsync` 中一次性读取当前批次的员工和班组人员名单。
- 列表人数列显示总数及员工/班组分项。
- 详情 JSON 携带分组名单，一级详情弹窗增加人数分项与名单入口。
- 新增二级名单弹窗，并在工资工作台脚本中安全渲染姓名、班组和金额。
- 覆盖工资工具栏的溢出规则，确保列管理菜单可见；覆盖日期和人数单元格的换行与宽度规则。

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~PayrollDisbursementPageTests|FullyQualifiedName~PayrollEditPageModelTests"
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 build .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --configuration Release
```

- 重启本地服务并检查 `/health/ready`。
- 在桌面浏览器中验证全部、公司、私人转账及状态组合筛选。
- 检查左侧汇总与列表一致、页面无重叠、控制台无新增错误。
- 执行 `git diff --check` 并确认没有修改任务范围外文件。
