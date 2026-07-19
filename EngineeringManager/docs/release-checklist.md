# 发布检查清单

## 本地交付验证（2026-07-20）

- [x] Release 构建 0 警告、0 错误；全量测试 `592/592` 通过。
- [x] EF `migrations has-pending-model-changes` 报告无待处理模型变化。
- [x] `20260719182035_CentralLedgerMultiEntryFinance` 已应用到 `EngineeringManager_Test`，未操作生产库。
- [x] 旧财务 126 条稳定映射幂等复跑数量不变；数量、金额、公司/合作商/项目/合同/方向汇总差异为 0，未分摊金额为 0。
- [x] 54 条工程量疑似重复项生成预检报告并保持不回填，其中近似金额候选 3 条、无候选 51 条。
- [x] 数据库与附件备份已完成恢复演练，`BACKUP_RESTORE=PASS`，临时恢复库已删除。
- [x] 桌面 1440px、手机 390px 已验证外部/内部账本、统一录入、财务年度、对账、项目、班组与合作商入口；无页面级横向溢出、框架错误覆盖层或控制台错误。
- [x] 重新生成 Release 发布包并确认 `PUBLISH_RELEASE=PASS`。
- [x] 重新执行完整质量门禁并确认 `QUALITY_GATE=PASS`。

## 生产部署前检查

- [ ] Release 完整质量门禁 0 失败、0 警告、0 错误。
- [ ] EF 无待迁移模型变化，迁移脚本经过审核。
- [ ] 发布包包含 Web DLL、web.config、静态资源、Manifest 和 Service Worker。
- [ ] 生产连接字符串只由 IIS/环境变量提供。
- [ ] `DevelopmentSampleData:Enabled` 在生产配置中为 false，测试账号和凭据文件不复制到生产。
- [ ] 正式数据库与附件已完成异机备份，恢复步骤已演练。
- [ ] 域名、TLS、应用池身份、目录权限、日志、监控和回滚负责人已确认。
- [ ] `EngineeringManager_Test` 仅在实际生产部署确认后人工删除。
