# 阶段 6：导入、导出、备份与提醒实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 提供标准 Excel 导入导出、自由字段与保存设置、老系统字段映射框架、数据库和附件备份任务，以及系统内提醒中心。

**Architecture:** Application 定义导出数据集、字段目录、筛选条件和导入预览接口；Infrastructure 使用项目内轻量 XLSX 读写器生成/解析标准表格，不增加外部依赖；EF Core 保存导入批次、导出模板、备份任务和提醒记录；备份通过可替换执行器调用 SQL Server BACKUP 并打包附件。页面只编排服务和下载文件，不直接拼装 Excel、执行 SQL 或计算风险。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、System.IO.Compression、System.Xml.Linq、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：导出字段、筛选和模板规则

**Files:**
- Create: `src/EngineeringManager.Domain/DataExchange/DataExchangeEnums.cs`
- Create: `src/EngineeringManager.Domain/DataExchange/ExportSelectionValidator.cs`
- Modify: `src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Create: `tests/EngineeringManager.Tests/Domain/ExportSelectionValidatorTests.cs`

- [ ] 写测试，确认自由字段顺序保留、重复/未知字段拒绝、默认字段可恢复。
- [ ] 写测试，确认筛选截止日和模板可见范围合法，个人模板与共享模板边界明确。
- [ ] 运行测试确认类型尚不存在而失败。
- [ ] 实现导出数据集、字段类型、模板范围、任务状态和选择校验规则。
- [ ] 运行领域测试确认通过。

## Task 2：轻量 XLSX 读写器

**Files:**
- Create: `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxWorkbook.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/SimpleXlsxReader.cs`
- Create: `tests/EngineeringManager.Tests/Infrastructure/SimpleXlsxWorkbookTests.cs`

- [ ] 写测试，确认可生成包含总览汇总和项目明细的多工作表 XLSX。
- [ ] 写测试，确认文本、日期、整数和小数往返读取，中文工作表和单元格不乱码。
- [ ] 运行测试确认读写器尚不存在而失败。
- [ ] 使用 ZIP + Open XML 最小结构实现，不引入外部 NuGet 依赖。
- [ ] 运行读写测试确认 Excel 标准文件可往返。

## Task 3：项目经营总览自由导出与保存设置

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/ExportTemplate.cs`
- Create: `src/EngineeringManager.Application/DataExchange/ExportDtos.cs`
- Create: `src/EngineeringManager.Application/DataExchange/IExportService.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Application/ProjectOverviewExportTests.cs`

- [ ] 写测试，确认默认导出包含总览汇总和每项目一行的项目明细。
- [ ] 写测试，确认应收、已收、未收、应开票、已开票、未开票和项目基础信息可自由选择并排序。
- [ ] 写测试，确认用户上次设置自动保存，个人多模板和管理员共享模板可读取。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现项目经营总览导出、字段目录、筛选截止日、模板保存和 XLSX 下载结果。
- [ ] 运行导出测试确认通过。

## Task 4：员工、合作单位、工资和财务通用导出

**Files:**
- Modify: `src/EngineeringManager.Application/DataExchange/ExportDtos.cs`
- Modify: `src/EngineeringManager.Application/DataExchange/IExportService.cs`
- Modify: `src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Create: `tests/EngineeringManager.Tests/Application/ModuleExportTests.cs`

- [ ] 写测试，确认员工、合作单位、工资批次、收付款和账户流水可导出。
- [ ] 写测试，确认每个数据集拥有独立字段目录和上次设置，不互相覆盖。
- [ ] 运行测试确认数据集尚未支持而失败。
- [ ] 实现模块导出和通用字段投影。
- [ ] 运行通用导出测试确认通过。

## Task 5：标准导入模板、预览和错误报告

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/ImportBatch.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/ImportError.cs`
- Create: `src/EngineeringManager.Application/DataExchange/ImportDtos.cs`
- Create: `src/EngineeringManager.Application/DataExchange/IImportService.cs`
- Create: `src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Application/StandardImportTests.cs`

- [ ] 写测试，确认员工、合作单位和项目标准模板可生成并预览。
- [ ] 写测试，确认必填、重复、枚举和关联错误按行列返回，不产生半批数据。
- [ ] 写测试，确认自定义老系统字段映射只作用于指定批次并保留原始文件元数据。
- [ ] 运行测试确认导入服务尚不存在而失败。
- [ ] 实现模板、预览、错误报告和确认导入事务。
- [ ] 运行导入测试确认通过。

## Task 6：数据库与附件备份任务

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/BackupTask.cs`
- Create: `src/EngineeringManager.Application/Backups/BackupDtos.cs`
- Create: `src/EngineeringManager.Application/Backups/IBackupService.cs`
- Create: `src/EngineeringManager.Infrastructure/Backups/SqlServerBackupExecutor.cs`
- Create: `src/EngineeringManager.Infrastructure/Backups/BackupService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Application/BackupServiceTests.cs`

- [ ] 写测试，确认手动备份创建数据库备份请求、附件 ZIP 和任务结果记录。
- [ ] 写测试，确认失败任务保存错误信息且不会标记成功。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现可替换 SQL Server 备份执行器、附件打包、下载元数据和清晰状态。
- [ ] 运行备份测试确认通过。

## Task 7：提醒中心和失败风险提示

**Files:**
- Create: `src/EngineeringManager.Infrastructure/Data/ReminderItem.cs`
- Create: `src/EngineeringManager.Application/Reminders/ReminderDtos.cs`
- Create: `src/EngineeringManager.Application/Reminders/IReminderService.cs`
- Create: `src/EngineeringManager.Infrastructure/Reminders/ReminderService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `tests/EngineeringManager.Tests/Application/ReminderServiceTests.cs`

- [ ] 写测试，确认项目节点、应收未收、应付未付、未开票、工资未发和失败任务生成提醒。
- [ ] 写测试，确认提醒可标记已读/已处理且重复刷新不会无限重复。
- [ ] 运行测试确认服务尚不存在而失败。
- [ ] 实现系统内提醒刷新、列表和状态更新。
- [ ] 运行提醒测试确认通过。

## Task 8：页面、迁移与阶段验收

**Files:**
- Modify: `src/EngineeringManager.Web/Program.cs`
- Modify: `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Backups/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Backups/Index.cshtml.cs`
- Create: `src/EngineeringManager.Web/Pages/Reminders/Index.cshtml`
- Create: `src/EngineeringManager.Web/Pages/Reminders/Index.cshtml.cs`
- Create: `tests/EngineeringManager.Tests/Web/DataExchangeBackupAuthorizationTests.cs`
- Create: `src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_DataExchangeBackupsReminders.cs`
- Modify: `README.md`
- Modify: `docs/开发进度.md`

- [ ] 写 Web 测试，确认查询人员可导出但不能导入/备份，管理员可共享模板和备份，财务可导出财务/工资数据。
- [ ] 实现导入导出、备份和提醒页面及下载端点。
- [ ] 创建并应用阶段 6 Migration 到本机 SQL Server。
- [ ] 运行完整质量门禁和真实 HTTP 验收。
- [ ] 更新唯一进度文件，记录迁移、测试数、模板范围、备份边界和阶段 7 计划。

## 阶段 6 完成定义

- 项目经营总览默认导出含汇总页和项目明细页，用户可以自由选字段、排序并保存设置。
- 员工、合作单位、工资和财务主要数据集可使用标准 Excel 导出，标准模板可预览导入错误。
- 老系统迁移使用批次级字段映射和错误清单，不建设复杂通用映射平台。
- 数据库和附件可形成一组可追踪备份任务及下载产物。
- 首页/提醒中心覆盖经营风险和导入、导出、备份失败，不发送外部通知。
