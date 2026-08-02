# 全局主题 Design QA

- 日期：2026-08-02
- 服务地址：`http://127.0.0.1:5075`
- 参考图：`C:/Users/TZM-NEW/AppData/Local/Temp/codex-clipboard-d4f7d9dd-366b-47e8-812b-3ee631526e4c.jpg`
- 实现截图（桌面登录页）：`C:/Users/TZM-NEW/AppData/Local/Temp/engineering-manager-theme-qa/01-login-desktop.png`
- 实现截图（移动登录页）：`C:/Users/TZM-NEW/AppData/Local/Temp/engineering-manager-theme-qa/02-login-mobile.png`

## 参考与实现证据

![参考主题](C:/Users/TZM-NEW/AppData/Local/Temp/codex-clipboard-d4f7d9dd-366b-47e8-812b-3ee631526e4c.jpg)

![桌面实现](C:/Users/TZM-NEW/AppData/Local/Temp/engineering-manager-theme-qa/01-login-desktop.png)

![移动实现](C:/Users/TZM-NEW/AppData/Local/Temp/engineering-manager-theme-qa/02-login-mobile.png)

> 实现截图中的登录字段由本机浏览器自动填充；截图仅保存在本机临时目录，没有加入 Git。

## 验收状态

| 步骤 | 视口 / 状态 | 结果 |
| --- | --- | --- |
| 1. 登录页桌面检查 | 1440 × 900，默认主题 | 通过：页面身份正确、内容完整、无框架错误层、控制台无警告或错误 |
| 2. 登录页移动检查 | 390 × 844，默认主题 | 通过：单列布局可读、页面可滚动、无横向内容溢出、控制台无警告或错误 |
| 3. 设置页三主题切换 | `/Admin/Settings` | 阻塞：当前浏览器尚未完成登录，未提交或绕过本机凭据 |
| 4. 保存后全站保持 | `/Projects` 与代表性表格、表单、弹窗页面 | 阻塞：需要已登录会话 |
| 5. 三主题桌面 / 移动对比 | 科技商务、清透毛玻璃、薰衣草奶油 | 自动化与代码覆盖通过；实际受保护页面截图待登录后补验 |

## 已完成的主题审计

- 三个主题均有稳定 CSS class，薰衣草主题沿用现有 `Display.Theme` 持久化键。
- 默认主题、清透毛玻璃和薰衣草奶油均定义或继承完整全局令牌；半透明主色效果使用 `--app-primary-rgb`。
- 毛玻璃主题覆盖设置卡、普通表格、数据工作台、提示层和弹窗，同时保留设备页嵌入式透明工具栏。
- 薰衣草主题覆盖侧栏、页头、卡片、表格、表单、按钮、状态色、focus、hover 与弹窗表面。
- 主题卡为 3 / 2 / 1 列响应式布局，主题、动效和特效单选框 ID 唯一。
- 主题预览同步浏览器主题色；字体、字号、动效、特效和表格密度均可即时预览。
- 紧凑与宽松表格密度同时覆盖 `.data-table` 和普通 `.table-wrap > table`。
- 非法主题、动效、特效、字体、字号或表格密度会在写入前被拒绝；系统设置仍使用现有六个键保存。
- 高强度动效阴影和点击涟漪随当前主题主色变化，并尊重 `prefers-reduced-motion`。

## 参考图对比

- 已采用：柔和紫色侧栏、奶油白卡片、圆角与轻阴影、薄荷绿 / 粉色 / 天蓝点缀。
- 有意不复制：参考图上的短视频大字、头像悬浮层和社交平台交互控件。
- 当前限制：未登录前无法对设置页、项目列表和代表性业务页面进行同状态截图，因此不能宣称完整视觉对比通过。

final result: blocked
