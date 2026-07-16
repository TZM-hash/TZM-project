# 发布检查清单

- [ ] Release 完整质量门禁 0 失败、0 警告、0 错误。
- [ ] EF 无待迁移模型变化，迁移脚本经过审核。
- [ ] 发布包包含 Web DLL、web.config、静态资源、Manifest 和 Service Worker。
- [ ] 生产连接字符串只由 IIS/环境变量提供。
- [ ] `DevelopmentSampleData:Enabled` 在生产配置中为 false，测试账号和凭据文件不复制到生产。
- [ ] 正式数据库与附件已完成异机备份，恢复步骤已演练。
- [ ] 域名、TLS、应用池身份、目录权限、日志、监控和回滚负责人已确认。
- [ ] `EngineeringManager_Test` 仅在实际生产部署确认后人工删除。
