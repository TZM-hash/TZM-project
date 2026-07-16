# 阶段 9：设备管理实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付逐台设备档案、自有/租赁权属、项目使用日期段、租金计算、单次最终结算、财务关联、轻量维保及设备现场有限离线。

**Architecture:** 设备作为独立有界模块，项目、公司、合作单位和财务仍由现有模块持有权威数据。领域层只负责日期与金额纯计算；应用服务负责权限和状态；设备离线使用独立映射记录并复用阶段 7 的 IndexedDB、照片、幂等和冲突基础。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core SQL Server、IndexedDB、Service Worker、原生 JavaScript/Canvas、现有 SimpleXlsx、xUnit、FluentAssertions、SQLite 测试数据库。

---

## Task 1：设备领域枚举、日期和租金计算

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Domain/Equipment/EquipmentEnums.cs`
- Create: `EngineeringManager/src/EngineeringManager.Domain/Equipment/EquipmentUsageCalculator.cs`
- Create: `EngineeringManager/src/EngineeringManager.Domain/Equipment/EquipmentRentCalculator.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Domain/EquipmentCalculatorTests.cs`

- [ ] 写失败测试，覆盖进退场首尾包含、多段施工/停工、重叠拒绝、未分类天数、单段停工计租、日租/月租自然月/月租除 30/阶段包干和加减项。

```csharp
[Fact]
public void InclusivePeriodsClassifyConstructionStopAndUnclassifiedDays()
{
    var result = EquipmentUsageCalculator.Calculate(
        new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10),
        [new(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4), EquipmentPeriodType.Work, true),
         new(new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 7), EquipmentPeriodType.Stop, false)]);
    result.TotalDays.Should().Be(10);
    result.WorkDays.Should().Be(4);
    result.StopDays.Should().Be(3);
    result.UnclassifiedDays.Should().Be(3);
}
```

- [ ] 运行 `dotnet test --filter FullyQualifiedName~EquipmentCalculatorTests`，确认 RED。
- [ ] 实现纯领域计算和中文校验错误；金额统一 decimal，日期统一 `DateOnly`。
- [ ] 重跑测试确认 GREEN。

## Task 2：设备持久化模型与约束

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Equipment.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentLeaseAgreement.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentProjectUsage.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentWorkPeriod.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentSettlement.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentSettlementAdjustment.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentAdvancePayment.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentOwnershipHistory.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/EquipmentMaintenanceRecord.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/EquipmentModelTests.cs`

- [ ] 写 SQLite 失败测试，验证设备编号唯一、租赁出租方必填、自有设备所属公司必填、日期段级联范围、业务历史 Restrict、结算一对一和并发标记。
- [ ] 实现实体及配置；一条使用只关联一个项目和一家项目签约公司，最终结算最多一条。
- [ ] 对日期重叠使用服务校验和数据库事务保护；逻辑删除/状态历史不级联破坏财务。
- [ ] 重跑模型测试确认 GREEN。

## Task 3：设备档案、租赁约定、使用和复制服务

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Equipment/EquipmentDtos.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/Equipment/IEquipmentService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Equipment/EquipmentService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Program.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentServiceTests.cs`

- [ ] 写失败测试：自有/租赁建档、复制清空设备编号、租赁约定继承、进场自动使用中、退场自动闲置、重叠阻止、管理员共享使用例外和单公司归属。
- [ ] 定义服务接口，所有写入携带 actor、并发版本和修改原因。

```csharp
public interface IEquipmentService
{
    Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token);
    Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token);
    Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token);
    Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token);
}
```

- [ ] 实现数据范围、状态转换、日期校验、租赁默认值和审计原因传递。
- [ ] 重跑服务测试确认 GREEN。

## Task 4：最终结算、押金预付款和财务应付

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Application/Equipment/IEquipmentSettlementService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Equipment/EquipmentSettlementService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Finance/FinanceLedgerService.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentSettlementServiceTests.cs`

- [ ] 写失败测试：未退场/未分类不能终结算、基础租金与加减项、押金/预付款抵扣、押金退回、修改前后快照、生成应付幂等和已付款后差额调整。
- [ ] 实现单次最终结算；不建立月度中间结算或多段基础价格。
- [ ] `GeneratePayableAsync` 只在用户选择时创建一个来源为设备结算的应付；重复调用返回原记录。
- [ ] 结算变更不得改写付款流水，差额通过应付调整记录体现。
- [ ] 重跑结算与财务回归测试确认 GREEN。

## Task 5：权属、非必填维保、提醒和权限

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Equipment/IEquipmentService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Equipment/EquipmentService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Security/PermissionKeys.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Security/SystemRoles.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Domain/Reminders/ReminderEnums.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Reminders/ReminderService.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentOwnershipMaintenanceTests.cs`

- [ ] 写失败测试：内部/外部转让保留历史且不生成财务、购置资料可选、维保全字段可空、租赁/维保到期提醒生成与解决、其他设备提醒不生成。
- [ ] 新增设备查看、档案、进退场、结算、维保权限和 `EquipmentManager` 角色预设。
- [ ] 实现转让状态、维保 CRUD 和仅两类提醒。
- [ ] 重跑权限、提醒和设备服务测试确认 GREEN。

## Task 6：设备页面和图形化总览

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Index.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Index.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Details.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Details.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Edit.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Edit.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Usage.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Usage.cshtml.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Settlement.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Settlement.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/EquipmentPageTests.cs`

- [ ] 写页面与授权失败测试，覆盖指标卡、状态/项目分布、日期段编辑、复制、结算、角色权限和匿名保护。
- [ ] 实现响应式列表、详情、编辑、使用和结算页面；关键数值直接显示，手机无横向溢出。
- [ ] 在导航增加设备入口并显示阶段 9。
- [ ] 运行页面测试确认 GREEN。

## Task 7：设备 Excel 导入导出

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Domain/DataExchange/DataExchangeEnums.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ExportService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/DataExchange/ImportService.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentDataExchangeTests.cs`

- [ ] 写失败测试，覆盖设备档案、租赁约定、项目使用、日期段、结算模板导入预览/确认以及自由导出、上次设置和模板。
- [ ] 增加 `Equipment`、`EquipmentLeases`、`EquipmentUsages`、`EquipmentPeriods`、`EquipmentSettlements` 数据集。
- [ ] 导入严格校验设备/项目/公司/出租方引用和日期重叠，错误批次不写入半批数据。
- [ ] 重跑数据交换回归测试确认 GREEN。

## Task 8：设备现场离线服务与端点

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/OfflineEquipmentUsageSync.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/OfflineEquipmentAttachmentSync.cs`
- Create: `EngineeringManager/src/EngineeringManager.Application/EquipmentOffline/IEquipmentOfflineService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/EquipmentOffline/EquipmentOfflineService.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Offline.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Offline.cshtml.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentOfflineServiceTests.cs`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/EquipmentOfflineAuthorizationTests.cs`

- [ ] 写失败测试：客户端草稿/操作幂等、用户和项目权限、设备版本冲突、日期段校验、最多 20 张照片、照片独立重试及租金/结算字段不可离线提交。
- [ ] 实现 `(UserId, ClientDraftId)` 与照片客户端 ID 唯一约束，服务端重新计算日期并重校验重叠。
- [ ] 实现角色授权和防伪保护的 JSON/multipart handlers。
- [ ] 重跑离线服务与授权测试确认 GREEN。

## Task 9：IndexedDB 设备草稿、照片和安全 Service Worker

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/offline-equipment.js`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Offline.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/service-worker.js`
- Create: `EngineeringManager/tests/EngineeringManager.Tests/Web/EquipmentOfflineAssetsTests.cs`

- [ ] 写静态失败测试，确认用户分区、设备草稿/日期段/照片、20 张与 3 MB 策略、递增退避、冲突操作、清理本机数据和敏感路由排除。
- [ ] 复用阶段 7 IndexedDB 版本升级和照片压缩基础，新增设备 store/queue 类型，不复制第二个数据库。
- [ ] Service Worker 只缓存设备离线外壳和静态资源，不缓存租金、结算、应付、付款或发票响应。
- [ ] 重跑静态资产测试确认 GREEN。

## Task 10：迁移、浏览器验收、文档和提交

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/<timestamp>_EquipmentManagement.cs`
- Modify: `EngineeringManager/README.md`
- Modify: `docs/开发进度.md`

- [ ] 生成并应用 `EquipmentManagement` Migration 到 `EngineeringManager_Test`，检查表、外键和唯一索引。
- [ ] 运行阶段 9 定向测试和完整质量门禁，要求全部通过、Release 0 警告/0 错误。
- [ ] 使用本地测试管理员和样例数据验证设备建档、进退场、日期段、结算、财务关联和离线页面；验证桌面与 390px、控制台和匿名泄露。
- [ ] 更新 README、总体设计和唯一进度文档，记录迁移、测试总数、离线范围和阶段 10 计划。
- [ ] 本地提交阶段 9，不推送远端。

## 阶段 9 完成定义

- 逐台设备、自有/租赁、项目日期段、单次终结算、应付关联、权属和非必填维保完整可用。
- 设备现场记录可有限离线，租金/结算/财务保持在线。
- Excel、权限、提醒和图形化页面与现有系统一致集成。
- Migration 只应用到 `EngineeringManager_Test`，完整质量门禁与真实浏览器验收通过并完成本地提交。
