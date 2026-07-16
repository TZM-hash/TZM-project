# 测试库备份恢复验证

- 源数据库固定为 `EngineeringManager_Test`。
- 临时恢复数据库固定为 `EngineeringManager_RestoreVerification`。
- 脚本执行 SQL Server CHECKSUM 备份，恢复后核对迁移历史和核心公司数据，再删除临时恢复库。
- `EngineeringManager_Test` 不会被覆盖或删除；附件同时生成 ZIP 清单。
- 2026-07-17 实际结果：`BACKUP_RESTORE=PASS`；恢复数据库已删除。
- 数据库备份：`App_Data/backups/EngineeringManager_Test_20260717044604.bak`。
- 附件备份：`App_Data/backups/EngineeringManager_Attachments_20260717044604.zip`。
- 运行命令：`& .\scripts\verify-backup-restore.ps1`。
