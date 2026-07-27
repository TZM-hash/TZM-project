# 合作单位工作台实施计划

**目标：** 将合作单位列表重排为与设备管理一致的左右工作台，使用 Mac 风格弹窗完成新增、查看、编辑和复制，同时保留财务页面跳转，以停用代替删除。

**边界：** 不修改合作单位服务接口、数据库结构和财务业务页；保留 `/Partners/Create` 与 `/Partners/Details` 兼容旧链接；不增加图表或物理删除。

**技术栈：** ASP.NET Core Razor Pages、原生 JavaScript、现有 DataWorkbench 与 CSS 设计系统、xUnit/FluentAssertions。

---

### 任务 1：锁定页面与权限契约

**文件：**
- 修改：`EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerStageResultAuthorizationTests.cs`
- 修改：`EngineeringManager/tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- 新建：`EngineeringManager/tests/EngineeringManager.Tests/Web/PartnerWorkspacePageTests.cs`

- [ ] 增加失败测试，要求左右工作台、角色/状态筛选、精简列、四个语义按钮和 Mac 弹窗存在。
- [ ] 更新旧行内编辑断言，要求合作单位不再渲染行内编辑器。
- [ ] 增加管理员与只读角色的操作入口断言。
- [ ] 运行定向测试并确认因功能尚未实现而失败。

### 任务 2：实现服务端工作台模型

**文件：**
- 修改：`EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml.cs`

- [ ] 增加角色、状态 GET 筛选与全量汇总数据。
- [ ] 用统一 `Editor` 输入模型替换 `QuickEdit`，在同一页面处理新增和编辑。
- [ ] 新增使用 `CreateAsync`，编辑使用 `UpdateAsync`；保留角色、主要联系人、状态、并发戳和修改原因。
- [ ] 权限不足时禁止保存；校验错误时重新加载工作台并自动打开编辑弹窗。

### 任务 3：重排页面与交互

**文件：**
- 修改：`EngineeringManager/src/EngineeringManager.Web/Pages/Partners/Index.cshtml`
- 新建：`EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/partner-workspace.js`
- 修改：`EngineeringManager/src/EngineeringManager.Web/Pages/Shared/DataWorkbenchPresets.cs`

- [ ] 左侧显示总数、启用、停用、参与项目及四类角色数量，并可按角色筛选。
- [ ] 右侧使用搜索、角色、状态筛选和精简表格。
- [ ] 新增查看、编辑、复制横向弹窗；复制沿用现有副本规则并清空信用代码及联系人。
- [ ] 财务按钮继续跳转 `/Partners/Details`，不增加删除入口。

### 任务 4：样式与响应式

**文件：**
- 修改：`EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`

- [ ] 左右卡片按较高一侧等高，左侧不锁死高度。
- [ ] 长单位名称两行截断并保留 `title` 完整提示。
- [ ] 查看、编辑、复制、财务使用蓝、橙、紫、青语义颜色并保持单行。
- [ ] 桌面弹窗横向双列，窄屏改为单列；表格只在自身容器内滚动。

### 任务 5：验证

- [ ] 运行合作单位、响应式和权限定向测试。
- [ ] 运行 Release 构建并确保零错误。
- [ ] 浏览器验证搜索/角色/状态筛选、新增/查看/编辑/复制、财务跳转边界。
- [ ] 在 `390x844` 验证单列布局、弹窗和横向溢出。
- [ ] 检查控制台、`git diff --check` 和最终工作区差异。
