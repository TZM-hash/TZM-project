# 项目备注日期回填实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从项目备注中的机器生命周期日期安全补齐项目实际开工和实际完工日期，并生成可审计、可重复执行的维护报告。

**Architecture:** `ProjectNoteDateParser` 负责把备注拆成带上下文的完整日期候选；`ProjectDateBackfillService` 负责项目级取最早进场和最晚完工候选、跳过不安全记录、在事务中只补空字段并记录审计。Web 启动程序新增独立维护参数，复用 SQL Server 备份执行器后执行服务并写 JSON 报告。

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, SQL Server, SQLite test provider, xUnit, FluentAssertions.

---

### Task 1: 日期解析器失败测试

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectNoteDateParserTests.cs`

- [x] **Step 1: 写失败测试**

覆盖：标准中文/点号/紧凑日期，机器进场与退场候选，付款日期排除，无年份不产生候选，完工早于开工可被识别为异常。

- [x] **Step 2: 运行测试确认因类型不存在而失败**

运行：`dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectNoteDateParserTests`

预期：编译失败，提示 `ProjectNoteDateParser` 尚不存在。

### Task 2: 实现解析器

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ProjectNoteDateParser.cs`

- [x] **Step 1: 实现日期候选模型与正则解析**

解析完整年份日期，保留原文和上下文位置；按工程生命周期词分类开始/结束候选，过滤财务词上下文。

- [x] **Step 2: 运行解析器测试**

运行同 Task 1 命令，预期全部通过。

### Task 3: 回填服务失败测试

**Files:**
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectDateBackfillServiceTests.cs`

- [x] **Step 1: 写 SQLite 夹具测试**

验证只填空字段、已有日期不覆盖、完工早于开工进入待核实、首次执行有变更且第二次执行为零变更。

- [x] **Step 2: 运行测试确认失败**

运行：`dotnet test EngineeringManager/tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectDateBackfillServiceTests`

预期：编译失败，提示回填服务尚不存在。

### Task 4: 实现事务回填与审计

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ProjectDateBackfillService.cs`

- [x] **Step 1: 实现计划与回填结果模型**

加载所有项目，解析备注并生成逐项目报告；只对空字段和安全候选生成变更。

- [x] **Step 2: 实现事务写入**

使用 `BeginTransactionAsync`，更新 `UpdatedAt`/`ConcurrencyStamp`，写入 `AuditLog`，保存并提交；无变更时不写入。

- [x] **Step 3: 运行服务测试**

运行 Task 3 命令，预期全部通过。

### Task 5: 接入维护参数

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/MaintenanceModeParser.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/MaintenanceModeParserTests.cs`

- [x] **Step 1: 增加 `--backfill-project-dates-from-notes` 精确参数及互斥测试**
- [x] **Step 2: 注册服务并在命令分支中先备份、再回填、最后写 UTF-8 JSON 报告**
- [x] **Step 3: 运行维护参数与相关测试**

### Task 6: 真实测试库预览、回填与审计

**Files:**
- Runtime only: `EngineeringManager_Test` database and ignored `App_Data` report/backup output.

- [x] **Step 1: 构建 Release 并运行维护命令**

设置 `ASPNETCORE_ENVIRONMENT=Development`，确认连接 `localhost\\SQLEXPRESS / EngineeringManager_Test`；命令输出备份路径、报告路径和变更计数。

- [x] **Step 2: 只读审计**

查询项目总数、已填日期数、日期倒置数、XM0208 和首尾异常项目；确认备份和报告存在。

- [x] **Step 3: 再次运行维护命令或服务预览**

确认第二次执行变更数为 0，既有日期不被覆盖。

### Task 7: 完整验证

- [x] 运行相关测试、完整 `dotnet test EngineeringManager/EngineeringManager.sln`。
- [x] 运行 `dotnet build EngineeringManager/EngineeringManager.sln -c Release --no-restore`。
- [x] 检查 Git diff，排除 `.build-obj/`、报告和备份等运行产物。
