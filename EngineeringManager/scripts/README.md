# 维护脚本说明

## 历史项目负责人回填

脚本 backfill-project-responsible-employees.ps1 从 old-data/旧资料项目导入模板_20260719.xlsx 的“项目导入”工作表读取：

- 项目名称：按名称唯一匹配当前项目。
- 原始_项目经理：解析为一个或多个负责人，并按原文顺序保存。
- 赵鸿辉：手机号：保存负责人姓名和手机号。
- 沈健马罗杰、马罗杰， 张冬冬：保存为多人负责人。
- 裘华忠班组、张恒挂靠：分别规范为“裘华忠”和“张恒”。

缺失人员会创建为 YG#### 员工，员工类型为劳务员工，设置为启用并允许作为项目负责人。已有员工只补充负责人资格；只有员工电话为空时才补充来源手机号，不覆盖已有电话。

### 运行

先做只读预演：

    pwsh -NoLogo -NoProfile -File .\scripts\backfill-project-responsible-employees.ps1 -Preview

正式执行：

    pwsh -NoLogo -NoProfile -File .\scripts\backfill-project-responsible-employees.ps1

脚本只允许写入 _Test 结尾的数据库，并且要求 Development 配置中的数据库名与参数一致。正式执行前自动创建 SQL Server 全量备份，写入在一个事务中完成；报告保存到 src/EngineeringManager.Web/App_Data/logs，备份保存到 src/EngineeringManager.Web/App_Data/backups。

脚本支持重复执行。没有新增或变化时不会重复创建员工、负责人关联或审计记录，但每次正式执行仍会先创建一份新的数据库备份。

### 回滚

如果需要回滚本次正式执行，保留脚本输出中的 .bak 路径，先停止 Web 服务，再使用项目现有的 restore-backup.ps1 恢复到测试库。恢复前应确认目标数据库名称和备份文件路径，恢复完成后重新应用需要的迁移并检查健康状态。

不要直接删除负责人关联或员工档案来回滚，因为这些记录可能已被其他业务数据引用。

