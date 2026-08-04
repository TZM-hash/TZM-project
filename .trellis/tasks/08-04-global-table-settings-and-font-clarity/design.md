# 全站列管理保存与字体清晰度优化设计

## 目标

修复共享列表列管理的本地设置被服务器视图覆盖的问题，并消除鼠标悬停数据面板时因面板级变换导致的文字模糊；保持既有布局、业务数据、字号设置和上一轮项目相关改动不变。

## 根因

`wwwroot/js/components/data-table.js` 的 `initialState` 当前先读取 `data-saved-view-columns`，只有服务器列为空时才读取 `localStorage`。因此服务器视图不是用户最后一次列管理设置时仍会覆盖它。

`wwwroot/css/themes.css` 当前在两种动效模式下对 `.panel:hover` 设置 `transform`。项目管理和多个工作区用 `.panel` 承载表格正文，鼠标进入后整块文本进入变换合成层，产生暂时性发虚。

## 技术设计

### 列状态

- 保持 `engineering-manager-workbench:<pageKey>:<tableId>` 本地键和现有 JSON 状态结构。
- 共享 Razor 局部视图增加“当前是否明确选择服务器保存视图”的数据标记。
- `initialState` 在普通进入时使用本地列状态优先；明确选择保存视图时使用服务器列状态，并在初始化完成后写入本地状态。
- `normalizeColumns` 作为唯一兼容入口，继续处理固定列、新增列和未知列。
- 现有确认、取消、恢复默认和 `localStorage` 异常保护逻辑保持职责不变。

### 字体清晰度

- 仅移除 Technology/Apple 动效下 `.panel:hover` 的 `transform`。
- 保留 `.metric-card:hover` 变换和面板非变换的阴影/颜色反馈。
- 不改全局字号和主题背景，避免引入布局变化或文本溢出。

## 验证

先添加并运行失败的资产回归测试，再实现最小修改；之后运行目标测试、完整构建、服务健康检查及浏览器悬停/列恢复检查。
