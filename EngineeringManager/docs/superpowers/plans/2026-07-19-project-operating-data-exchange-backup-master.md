# 项目经营、全业务 Excel 交换与系统安全实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans and superpowers:test-driven-development. Execute tasks serially in this workspace. Do not pause for user confirmation during implementation. Git commits, pushes, branch changes and production database operations remain forbidden unless separately authorized.

**Goal:** 在现有工程项目经营系统基础上，完成项目经营展示增量优化、全业务 Excel 数据交换、设置/完整备份、定时 NAS 副本、跨服务器安全恢复，并为所有主要业务大类提供可授权的全字段搜索。

**Architecture:** 继续复用现有模块服务、筛选状态、列管理、个人视图、财务汇总、审计、并发和附件存储能力。数据交换使用显式数据集目录和后台任务，不直接反射暴露数据库；备份使用数据库备份、附件清单、持久化计划和独立恢复工具。员工/工资合并已完成，统一财务账本仍作为独立后续模型依赖，不为旧财务模型建设长期模板。

**Tech Stack:** ASP.NET Core Razor Pages、EF Core、SQL Server、SQLite 测试、C#、原生 JavaScript、现有 XLSX 工具、PowerShell 7、Windows Task Scheduler/IIS、xUnit、FluentAssertions。

**Source specifications:**

- `docs/superpowers/plans/2026-07-18-project-operating-ui-incremental-optimization.md`
- `docs/superpowers/specs/2026-07-19-full-business-excel-data-exchange-design.md`
- `docs/superpowers/specs/2026-07-19-system-backup-restore-design.md`

**Incremental search and migration design (2026-07-19):**

- 搜索框支持空格分隔的多个关键词；每个关键词必须命中该模块允许搜索字段中的至少一个字段，字段之间为 OR、关键词之间为 AND。
- 搜索只在数据库查询阶段执行，不把完整表加载到内存后再过滤；保留每个模块现有的类型、状态、阶段、权限和分页筛选。
- 第一批全字段范围覆盖员工、项目、合作单位、自有公司、设备、施工班组、员工证书和公司证书，并包含已登记的关联对象名称、联系人、备注、日期、枚举和金额字段。
- 身份证号、银行卡号、工资和账户号等敏感字段仅在当前用户已有敏感信息权限时参与检索；普通用户搜索这些值不返回命中，也不记录原始搜索词。
- 每个模块保留独立筛选状态和最后一次选择；不引入跨模块隐式筛选或全局大表反射查询。
- 导入导出页依赖的 `DataExchangeTasks`、`BackupSchedules`、`ImportMappingTemplates` 等迁移必须在测试库先应用并在发布检查中阻止“页面已发布但迁移未应用”的状态；不改变生产库人工迁移规范。

---

## 0. Baseline and boundaries

- [ ] Confirm Release build, full test suite and quality gate are green after the completed temporary-worker merge.
- [ ] Preserve the unrelated untracked `old-data/` directory; do not inspect, move, delete or import it without an explicit data-migration task.
- [ ] Do not modify production databases. All migrations and recovery drills use `EngineeringManager_Test` or disposable `_Test` databases/directories.
- [ ] Do not commit, push, create branches, or rewrite Git history.
- [ ] Do not duplicate the already completed project stage/tax/equipment/temporary-worker features.
- [ ] Do not implement the separate central-finance ledger, allocation, annual reconciliation and settlement-point model in this plan. Finance Excel datasets wait for that model to stabilize.
- [ ] Do not add generic batch business operations; Excel import is a dedicated all-or-nothing data-exchange workflow.

## 1. Project operating UI and existing finance-summary reuse

### 1.1 Shared finance query

Modify:

- `src/EngineeringManager.Application/Finance/IFinanceLedgerService.cs`
- `src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- `tests/EngineeringManager.Tests/Application/FinanceSummaryTests.cs`
- `tests/EngineeringManager.Tests/Application/DashboardServiceTests.cs`

Implement an authorized-project overload of existing project summaries and make the dashboard consume it instead of duplicating refund, reversal, deduction and payroll-crew arithmetic. Preserve current permission filtering and empty-scope behavior. Add regression tests that compare dashboard totals with `FinanceLedgerService` for refunds, payment reversals, deductions and crew disbursement.

### 1.2 Project detail display

Create/modify:

- `src/EngineeringManager.Web/Presentation/ProjectOperatingDisplay.cs`
- `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- `src/EngineeringManager.Web/wwwroot/css/pages.css`
- `tests/EngineeringManager.Tests/Web/ProjectOperatingDisplayTests.cs`
- `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- `tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

Use existing `ProjectSummaryDto` and `FinanceProjectSummaryDto` to display current amount, settled, invoiced, collected, paid, uncollected, unpaid and cash gap. Keep the existing project stage and business tabs. Replace the existing payment mini-progress with settlement, invoice and collection progress. Do not add construction-progress management.

### 1.3 Dashboard cash watchlist

Modify:

- `src/EngineeringManager.Application/Dashboard/DashboardDtos.cs`
- `src/EngineeringManager.Infrastructure/Dashboard/DashboardService.cs`
- `src/EngineeringManager.Web/Pages/Index.cshtml`
- `src/EngineeringManager.Web/wwwroot/css/pages.css`
- `tests/EngineeringManager.Tests/Web/HomePageTests.cs`

Add authorized project cash rows sorted by cash gap and uncollected amount, showing stage, collected, paid, uncollected, unpaid and cash gap. Keep complete filtering and saved views in the existing Finance page instead of creating a second filter system. Preserve equipment, payroll, reminder and offline panels.

### 1.4 Conflict notice

Modify/create:

- `src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- `src/EngineeringManager.Web/wwwroot/js/components/conflict-notice.js`
- `src/EngineeringManager.Web/wwwroot/js/site.js`
- `src/EngineeringManager.Web/wwwroot/css/components.css`
- `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- `tests/EngineeringManager.Tests/Web/UiEffectsAssetTests.cs`

Reuse existing validation messages and concurrency stamps. Detect concurrency/business conflict text and show a closable alert dialog with refresh action. Never auto-overwrite, auto-retry or merge stale form data.

## 2. Data-exchange foundation and UI

### 2.1 Data-exchange domain contracts

Modify/create:

- `src/EngineeringManager.Domain/DataExchange/DataExchangeEnums.cs`
- `src/EngineeringManager.Application/DataExchange/ExportDtos.cs`
- `src/EngineeringManager.Application/DataExchange/ImportDtos.cs`
- `src/EngineeringManager.Application/DataExchange/IExportService.cs`
- `src/EngineeringManager.Application/DataExchange/IImportService.cs`
- Add explicit dataset/field/relationship/permission/attachment definitions under `src/EngineeringManager.Application/DataExchange/`
- Add task/mapping/package DTOs and tests under `tests/EngineeringManager.Tests/Application/`

Define explicit dataset metadata rather than exposing EF tables. Each field records key, label, type, import/export capability, sensitivity, required status, relationship, enum values, validation and dependency order. Add import source/mode, package format, attachment option, mapping template and task result contracts. Preserve existing DTO compatibility where existing pages still use them.

### 2.2 Filter and last-selection integration

Modify:

- `src/EngineeringManager.Application/DataViews/`
- `src/EngineeringManager.Infrastructure/DataViews/SavedDataViewService.cs`
- existing module list page models that already export current views
- corresponding saved-view and export tests

Store the last filter, sort, selected columns, column order, density, export scope, package format and attachment choice per user/page/dataset. Existing page-specific filters remain page-specific. Add reset-to-default and sanitize every stored key against the dataset definition.

### 2.3 Data-exchange page redesign

Modify/create:

- `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml`
- `src/EngineeringManager.Web/Pages/DataExchange/Index.cshtml.cs`
- supporting partials under `src/EngineeringManager.Web/Pages/DataExchange/`
- `src/EngineeringManager.Web/wwwroot/css/pages.css`
- `src/EngineeringManager.Web/wwwroot/js/site.js` and data-workbench components
- `tests/EngineeringManager.Tests/Web/DataWorkbenchPageTests.cs`
- `tests/EngineeringManager.Tests/Web/DataWorkbenchAssetTests.cs`

Split the page into Export, Import and Task History views. Export offers current view, custom fields, authorized full scope and multi-module selection; Import is a step-based source/upload/mapping/preview/confirm/result flow; history shows source, filters, fields, counts, attachments, status and downloadable error reports. Remove the current stacked field catalog/form layout.

### 2.4 Full-field search across main categories

Modify/create:

- `src/EngineeringManager.Infrastructure/Search/SearchTerms.cs`
- employee, project, partner, company, equipment, crew and certificate services
- corresponding list page models and `Index.cshtml` files
- `tests/EngineeringManager.Tests/Application/*ServiceTests.cs`
- `tests/EngineeringManager.Tests/Web/FullFieldSearchPageTests.cs`

- [x] Use whitespace-separated AND terms and field-level OR predicates in database queries.
- [x] Cover employee, project, partner, company, equipment, crew and both certificate categories, including registered relationship fields.
- [x] Preserve existing module-specific filters, saved project views, row authorization and pagination.
- [x] Restrict identity, bank account, wage and account-number search predicates to existing administrator/sensitive-data permissions.
- [x] Add search controls and full-field wording to pages that previously had no search entry.
- [x] Put the full-field search entry for Projects, Payroll and Finance inside the shared data-workbench toolbar; keep advanced filters available without duplicating the primary workflow.
- [x] Standardize every main-category list on one compact toolbar row: search and common filters on the left, saved view, advanced filters, column management, density, page size, save and export actions on the right; mobile wraps only when necessary.
- [x] Extend payroll disbursement search to batches, projects, companies, accounts, employees, crews, workers and notes with sensitive snapshot authorization.

### 2.5 Data-exchange migration release check

- [x] Apply `20260718203734_DataExchangeTaskBackupSchedules` to `EngineeringManager_Test`.
- [x] Verify `DataExchangeTasks`, `BackupSchedules` and `ImportMappingTemplates` exist and the migration is recorded.
- [x] Keep production migration manual; do not add startup schema mutation.

## 3. Excel export implementation

### 3.1 Stable datasets first

Implement complete field registries and exporters for these stable groups before finance-central-ledger completion:

- organization, legal entities, company accounts, company certificates and categories;
- business partners, roles and contacts;
- employees, employee affiliations, employee certificates and annual-ledger source records;
- projects, project companies, tax configurations, contracts, contract line items, milestones, assignments, partners and stage results;
- equipment, ownership, leases, usages, periods, maintenance and settlements.

Each exporter must apply authorization before query execution, use current service/read models, include only registered fields, and mark calculated fields read-only.

### 3.2 Payroll and crew datasets

After the completed temporary-worker merge, implement registries/exporters for payroll batches, payroll items, payroll payments, employee receipts, expenses, advances, other payments, construction crews, construction workers, memberships and payroll crew allocations. Use stable employee/crew IDs and current employee type labels. Do not reintroduce a temporary-worker dataset.

### 3.3 Finance dependency

Do not finalize long-lived finance import/export columns until the unified finance model is implemented and tested. Then add settlement, invoice, funds, allocation, annual and reconciliation datasets using the new source records only. Add migration-era compatibility export only if the unified finance plan requires a historical report.

### 3.4 Workbook/package builder

Extend `SimpleXlsxWorkbook` or add a focused package builder to support:

- single workbook with directory and one sheet per dataset;
- ZIP with `data-navigation.xlsx`, per-module workbooks, `manifest.json`, `checksums.sha256` and optional attachments;
- relative hyperlinks between records/workbooks and attachments;
- row counts, dataset version, exported filters and sensitivity notice sheet;
- stable safe filenames and no server physical paths.

Add round-trip tests for workbook links, ZIP links, selected modules, full authorized export and attachment metadata.

## 4. Excel import implementation

### 4.1 Standard templates and update exports

Generate blank templates with required fields, enum guidance, relationship key descriptions and an instruction sheet. Generated system-edit workbooks contain protected hidden system ID, concurrency version and operation metadata. Support new, update and mixed modes. Never physically delete through import.

### 4.2 Arbitrary Excel mapping

Implement automatic exact/alias/header mapping plus manual mapping UI. Allow saving personal/shared mapping templates. Require the user to select the source key and target dataset version. Reject stale/incompatible mappings instead of silently guessing.

### 4.3 Validation and all-or-nothing transaction

Stage original files and attachments; validate file/sheet/header/type/required/enum/relationship/permission/sensitivity/concurrency/business rules and attachment hashes before writing. When any row or attachment fails, import nothing and generate an Excel error report with source row, source column, target field, raw value, reason and correction guidance. On success, write datasets in dependency order inside a transaction, then atomically promote attachment files. Re-read all affected modules after commit.

### 4.4 Import history and audit

Persist source filename/hash, mapping, dataset versions, filters, actor, timestamps, new/update/unchanged/error counts, attachment count, result package and audit links. Provide downloads only through authorized endpoints.

## 5. Backup and restore implementation

### 5.1 Backup model and package

Modify/create:

- `src/EngineeringManager.Application/Backups/BackupDtos.cs`
- `src/EngineeringManager.Application/Backups/IBackupService.cs`
- `src/EngineeringManager.Infrastructure/Backups/BackupService.cs`
- `src/EngineeringManager.Infrastructure/Data/BackupTask.cs`
- new schedule, target, package manifest and restore-task entities
- EF migration and tests, only after test-database backup verification

Add independent settings/full backup types, package paths, local/NAS statuses, hashes, retention flags, schedule state and restore records. Settings package includes organization, business years, categories, display settings, user basics, roles, permissions and backup strategy, but no plaintext password, token, connection string or server key. Full package includes SQL Server backup, attachments, settings snapshot, version/migration manifest and checksums.

### 5.2 Schedule and storage

Implement manual, interval and fixed-time schedules independently for settings and full backups. Store next/last run, timezone, retention count, local target, NAS target, failure state and catch-up policy. Execute outside page requests via a persistent runner/Windows scheduled task integration with task locks. Generate locally, checksum, atomically move locally, copy to NAS temporary name, checksum, then rename. Record “local success/NAS failure” separately from full success.

### 5.3 Backup page

Modify `src/EngineeringManager.Web/Pages/Backups/Index.cshtml` and code-behind into Overview, Settings Backup, Full Backup, Schedule and Restore History. Add download, verify, retain, delete, manual-run and target-health actions with administrator authorization and audit logging.

### 5.4 Settings restore

Support upload/select, package/hash/version validation, category diff, selected-category restore, conflict preview, transactional apply and audit. Do not modify business data, attachments, connection strings, TLS or server keys.

### 5.5 Full restore tool

Create a standalone PowerShell/.NET restore runner under `scripts/` or a dedicated tool project. It must validate package/version/space/permissions, enter maintenance mode, create a pre-restore safety backup, release connections, restore SQL Server database and attachments, apply compatible migrations, run health/count/relationship checks, and exit maintenance mode only on success. Roll back or leave maintenance mode with a clear failure record when a step fails. Never target production automatically; require an explicit `_Test` database during automated verification.

## 6. Verification and delivery

- [ ] Run focused TDD tests after each task.
- [ ] Test dataset registry coverage: every user-maintainable field is registered or has a documented exclusion reason.
- [ ] Test export current view/full/custom/multi-module/single-workbook/ZIP/attachments/hyperlinks.
- [ ] Test blank-template new import, system export update, mixed import and arbitrary column mapping.
- [ ] Test type/reference/permission/sensitivity/concurrency/attachment errors and all-or-nothing behavior.
- [ ] Test personal/shared templates and last-selection restoration.
- [ ] Test backup manual/interval/fixed-time, task lock, catch-up, retention, local/NAS partial success, hash corruption and audit.
- [ ] Test settings diff/restore and full restore to disposable database plus temporary attachment root.
- [ ] Run existing representative performance baseline: common list/detail under 2 seconds, dashboard under 3 seconds, Excel export under 15 seconds.
- [ ] Run Release build, full test suite and `scripts/quality-gate.ps1`.
- [ ] Start the web app and verify desktop pages; after all implementation, perform the agreed 390px mobile acceptance.
- [ ] Update `docs/开发进度.md` only with observed results.

## 7. Execution order

1. Baseline and project operating UI.
2. Data-exchange contracts, task history, filter reuse and page redesign.
3. Stable master/project Excel exporters and templates.
4. Payroll/crew/equipment/attachment Excel exporters and importers.
5. Unified-finance implementation dependency, then finance Excel datasets.
6. Backup packages, schedules, NAS replication and page actions.
7. Settings restore, complete restore runner and cross-server test drill.
8. Full verification and delivery documentation.

## 8. Stop condition

Do not stop for ordinary implementation choices, test failures, stale generated artifacts or non-production environment issues. Diagnose, fix within scope, re-run verification and continue. Stop only at true external blockers such as missing source data required to define a field, unavailable SQL Server/NAS state after safe retries, or an explicit user instruction to pause.
