# 高级筛选统一弹窗实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将共享数据工作台的“高级筛选”从右侧抽屉改为与系统其他弹窗一致的居中弹窗，同时保留现有筛选、重置、关闭和提交行为。

**Architecture:** 保留现有 Razor 表单、查询参数和 `data-filter-drawer` 交互钩子，只移除抽屉定位并增加专用的居中筛选弹窗布局。弹窗内容区负责滚动，标题栏和操作栏固定；共享工作台对话框统一支持关闭按钮和点击遮罩关闭。

**Tech Stack:** ASP.NET Core Razor Pages、原生 `<dialog>`、CSS、ES modules、xUnit/FluentAssertions。

---

### Task 1: 更新共享工作台契约测试

**Files:**
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs`

- [x] **Step 1: 写出新的结构和样式契约断言**

将筛选断言调整为：共享 Razor 仍包含 `data-filter-drawer` 和 `data-close-dialog`，但不再包含 `workbench-drawer`；CSS 必须包含 `.filter-dialog`、`max-height: min(85dvh, 56rem)`、`grid-template-rows: auto minmax(0, 1fr) auto`，并保留通用 `.workbench-dialog::backdrop`。

- [x] **Step 2: 运行定向测试确认当前实现失败**

Run: `dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~DataWorkbenchAssetTests`

Expected: FAIL，因为当前 Razor/CSS 仍使用 `.workbench-drawer`，尚未提供 `.filter-dialog` 居中弹窗契约。

### Task 2: 将高级筛选改为居中弹窗

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_DataWorkbench.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/components.css`

- [x] **Step 1: 修改 Razor 弹窗类名**

把高级筛选对话框的类从 `workbench-dialog workbench-drawer` 改为 `workbench-dialog filter-dialog`，保留现有表单字段、隐藏参数、重置按钮、应用按钮和 `data-filter-drawer` 钩子。

- [x] **Step 2: 增加居中弹窗布局**

让 `.filter-dialog` 使用适中的响应式宽度、最大高度和 `overflow: hidden`；让其表单使用三行网格（标题、可滚动内容、底部操作），并让 `.workbench-filter-grid` 在内容过多时独立滚动。保留两列桌面布局和现有移动端单列布局，移动端弹窗宽度为视口减去安全边距而不是右侧贴边。

- [x] **Step 3: 删除抽屉专用 CSS**

移除 `.workbench-drawer` 及其移动端规则，避免任何页面继续以全高右侧抽屉渲染高级筛选。

### Task 3: 统一工作台弹窗关闭交互

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/data-table.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/components/filter-drawer.js`

- [x] **Step 1: 为工作台对话框增加遮罩点击关闭**

在 `initDialogs(root)` 中遍历工作台下的 `dialog`，保留 `[data-close-dialog]` 关闭按钮，并在 `event.target === dialog` 时调用 `dialog.close()`，使高级筛选和保存视图遵循同一关闭体验。

- [x] **Step 2: 防止重复打开并保持筛选钩子兼容**

高级筛选按钮只在弹窗存在且未打开时调用 `showModal()`；继续使用现有筛选 chip、清除、重置和表单提交逻辑，不改变查询参数名称或服务器筛选行为。

### Task 4: 验证

**Files:**
- No new files.

- [x] **Step 1: 运行工作台定向测试**

Run: `dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~DataWorkbenchAssetTests|FullyQualifiedName~DataWorkbenchPageTests`

Expected: PASS。

- [x] **Step 2: 运行完整测试和 Release 构建**

Run: `dotnet test EngineeringManager/EngineeringManager.sln --configuration Release`

Run: `dotnet build EngineeringManager/EngineeringManager.sln --configuration Release --no-restore`

Expected: 测试通过、构建 0 warnings/0 errors。

- [x] **Step 3: 检查服务健康状态**

Run: `Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5075/health/ready`

Expected: HTTP 200 且返回 `Healthy`。
