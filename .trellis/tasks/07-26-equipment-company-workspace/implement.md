# 设备管理按公司归类与合格证附件改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将设备管理改造成按自有公司筛选的工作台，补齐设备权属和采购信息，提供当前合格证附件，并让项目管理继续读取统一设备主数据。

**Architecture:** 扩展现有 `Equipment` 聚合及 `IEquipmentService`，增加独立管理公司和当前合格证字段，附件继续复用共享 `Attachment` 与 `IFileStore`。设备首页统一加载公司范围、汇总和弹窗表单；既有独立路由保留兼容，但列表操作全部改用 `<dialog>`。

**Tech Stack:** .NET 10、ASP.NET Core Razor Pages、EF Core 10、SQL Server/SQLite、原生 JavaScript、CSS、xUnit、FluentAssertions、Playwright

---

### Task 1: 扩展设备模型与共享契约

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Equipment.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/ApplicationDbContext.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Equipment/EquipmentDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Application/Equipment/IEquipmentService.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Infrastructure/EquipmentModelTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentServiceTests.cs`

- [ ] **Step 1: 写失败测试**

覆盖管理公司关系、采购字段、当前合格证元数据和附件关系的持久化；使用新请求签名测试完整设备信息往返。

- [ ] **Step 2: 运行测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentModelTests|FullyQualifiedName~EquipmentServiceTests" --no-restore
```

预期：新增属性和请求参数不存在导致编译失败。

- [ ] **Step 3: 实现实体与映射**

`Equipment` 增加 `ManagingLegalEntityId`/导航、`PurchaseDate`、`PurchaseAmount`、`QualificationCertificateNumber`、`QualificationIssuedOn`、`QualificationExpiresOn`、`QualificationAttachmentId`/导航。配置管理公司索引、两个 `Restrict` 外键、证书编号长度和金额精度。

- [ ] **Step 4: 扩展 DTO 和接口**

`SaveEquipmentRequest` 与 `EquipmentDetailsDto` 增加管理公司、显示名称、采购字段、证书字段、附件摘要和启用状态。接口增加：

```csharp
Task<EquipmentDetailsDto> GetEquipmentAsync(EquipmentActor actor, Guid id, CancellationToken token);
Task<CertificateFileDto> DownloadQualificationAttachmentAsync(EquipmentActor actor, Guid equipmentId, CancellationToken token);
```

`EquipmentFilter` 增加 `bool UnassignedOnly = false`；dashboard 增加证书临期/过期数。

- [ ] **Step 5: 运行模型测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentModelTests" --no-restore
```

预期：通过。

### Task 2: 实现保存、筛选和合格证附件

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Equipment/EquipmentService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Certificates/CertificateServiceSupport.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentOwnershipMaintenanceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentOfflinePhotoTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/EquipmentOfflineServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Performance/RepresentativeDataPerformanceTests.cs`

- [ ] **Step 1: 写行为失败测试**

覆盖管理公司必填与授权、自有/租赁字段互斥、按管理公司/待分配筛选、采购和证书搜索、复制语义、附件上传/下载/替换/删除以及无权访问。

- [ ] **Step 2: 运行测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentServiceTests|FullyQualifiedName~EquipmentOwnershipMaintenanceTests" --no-restore
```

- [ ] **Step 3: 更新设备服务**

构造函数注入 `IFileStore`；`AuthorizedEquipment` 纳入管理公司权限并保留产权公司/项目访问兼容。`GetEquipmentAsync` 加载管理公司、产权公司、出租方和附件；保存前验证公司权限、权属字段和证书日期。

- [ ] **Step 4: 复用证书附件逻辑**

使用 `CertificateServiceSupport.SaveAttachmentAsync`、`DownloadAsync`、`RemoveAttachmentAsync`，不复制文件类型和 20MB 限制。替换/删除更新审计快照；失败路径清理新文件，避免孤立附件。

- [ ] **Step 5: 实现 dashboard 分类**

`CompanyId` 只匹配管理公司，`UnassignedOnly` 只匹配空管理公司；搜索加入管理公司、采购和证书字段。证书状态使用 `CertificateExpiryCalculator`。

- [ ] **Step 6: 运行服务测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentServiceTests|FullyQualifiedName~EquipmentOwnershipMaintenanceTests|FullyQualifiedName~RepresentativeDataPerformanceTests" --no-restore
```

预期：通过。附件失败清理不通过时暂停后续页面实现。

### Task 3: 生成并验证 EF Core migration

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/*_EquipmentCompanyWorkspace.cs` (由 EF Core 命令生成唯一时间戳)
- Create: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/*_EquipmentCompanyWorkspace.Designer.cs` (与上述 migration 同时间戳)
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: 生成 migration**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\dotnet-tools\dotnet-ef.exe migrations add EquipmentCompanyWorkspace --project .\src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj --startup-project .\src\EngineeringManager.Web\EngineeringManager.Web.csproj --output-dir Data\Migrations
```

- [ ] **Step 2: 加入回填 SQL**

先用 `OwnerLegalEntityId` 回填；剩余设备使用最近一条 `EquipmentProjectUsages` 的 `LegalEntityId`，按 `EntryDate DESC, Id DESC` 选择。无法确定的保持空值。`Down` 只删除本次外键、索引和列。

- [ ] **Step 3: 检查范围并构建**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build .\EngineeringManager.sln --no-restore
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentModelTests" --no-restore
```

预期：migration 无无关表变化，构建和模型测试通过。

### Task 4: 建立设备工作台 PageModel 和弹窗处理器

**Files:**
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/EquipmentEditorInput.cs`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/_EquipmentEditor.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/_EquipmentDetailsDialog.cshtml`
- Create: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/_EquipmentUsageEditor.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Index.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Edit.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Usage.cshtml.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EquipmentPageTests.cs`

- [ ] **Step 1: 写 PageModel 失败测试**

验证公司选项加载、公司/待分配过滤、详情/编辑/复制局部 handler、`IFormFile` 到 `CertificateAttachmentUpload`、无效提交恢复选项与目标 dialog、附件授权下载。

- [ ] **Step 2: 运行 Web 测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentPageTests" --no-restore
```

- [ ] **Step 3: 提取统一输入和局部视图**

`EquipmentEditorInput` 实现 DataAnnotations、`From(dto, copy)` 和 `ToRequest(upload)`；复制清空 ID、设备编号、并发标记和附件 ID，保留管理公司、权属、采购及证书文本。

- [ ] **Step 4: 扩展 `IndexModel`**

统一加载 dashboard、公司、出租方和项目选项。POST 使用 PRG，保存后返回原筛选并显示提示；失败设置 `ActiveDialog` 并保留输入。GET 局部 handler 返回编辑、详情和进退场内容。

- [ ] **Step 5: 保持旧路由兼容**

`Edit/Details/Usage` 改用共享输入和 `GetEquipmentAsync`，作为直接 URL/无脚本回退；设备列表不再输出这些跳转链接。

- [ ] **Step 6: 运行 PageModel 测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentPageTests" --no-restore
```

预期：通过。

### Task 5: 重排首页并实现单实例弹窗

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Equipment/Index.cshtml`
- Modify: `EngineeringManager/src/EngineeringManager.Web/wwwroot/css/pages.css`
- Create: `EngineeringManager/src/EngineeringManager.Web/wwwroot/js/pages/equipment-workspace.js`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/EquipmentPageTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] **Step 1: 写结构与响应式失败测试**

断言公司筛选、左侧汇总、右侧紧凑列表和三个 dialog 容器存在；查看、编辑、复制、进退场使用 dialog 触发器而非独立页面链接；CSS 在桌面左右布局、窄屏单列。

- [ ] **Step 2: 重写设备工作台**

顶部保留新增和现场离线入口；公司范围含全部、指定公司、待分配。列表列为设备、型号/分类、管理公司、权属、状态、合格证、当前使用、操作，并以两行单元格压缩次要信息。

- [ ] **Step 3: 实现 `equipment-workspace.js`**

点击按钮 fetch 局部 HTML后 `showModal()`；支持关闭、加载失败、权属条件字段切换，并在载入编辑器后初始化附件选择/预览。管理公司始终显示，自有显示产权公司，租赁显示出租方。

- [ ] **Step 4: 添加作用域样式**

桌面使用 `minmax(230px, .32fr) minmax(0, 1fr)`；稳定表格列宽和操作区尺寸。`900px` 下单列，`680px` 下仅表格容器允许局部滚动。不得覆盖现有公司工作台样式。

- [ ] **Step 5: 运行页面测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~EquipmentPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore
```

预期：通过。

### Task 6: 接通项目管理的统一设备数据

**Files:**
- Modify: `EngineeringManager/src/EngineeringManager.Application/Projects/ProjectConstructionDtos.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Infrastructure/Projects/ProjectConstructionService.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `EngineeringManager/src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Application/ProjectConstructionServiceTests.cs`
- Modify: `EngineeringManager/tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] **Step 1: 写项目复用失败测试**

项目设备选项标签应包含管理公司和权属；项目内新建设备必须传管理公司并落到统一 `Equipment` 表，自有同时保存产权公司，租赁同时保存出租方。

- [ ] **Step 2: 运行项目测试确认失败**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectConstructionServiceTests|FullyQualifiedName~ProjectAuthorizationTests" --no-restore
```

- [ ] **Step 3: 扩展项目 DTO 和表单**

`CreateProjectEquipmentRequest` 增加 `ManagingLegalEntityId`；项目页从可访问自有公司选择管理公司，不再输入 GUID，并使用同样的自有/租赁条件字段规则。

- [ ] **Step 4: 更新选项投影**

标签格式为“设备编号 · 名称 · 管理公司 · 自有/租赁”；历史待分配设备显示“待分配公司”，项目内新增不允许产生待分配设备。

- [ ] **Step 5: 运行联合测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectConstructionServiceTests|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~EquipmentServiceTests" --no-restore
```

预期：通过。

### Task 7: 全量检查与浏览器验收

**Files:**
- Verify: all files listed in Tasks 1-6

- [ ] **Step 1: 检查格式与差异**

```powershell
$ErrorActionPreference = 'Stop'
dotnet format .\EngineeringManager.sln --verify-no-changes --no-restore
git diff --check
git status --short
```

预期：无格式错误；原有公司页面改动保留；migration 只包含设备新字段、索引和外键。

- [ ] **Step 2: 运行跨层定向测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~Equipment|FullyQualifiedName~ProjectConstruction|FullyQualifiedName~ProjectAuthorizationTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore
```

- [ ] **Step 3: 运行完整构建和测试**

```powershell
$ErrorActionPreference = 'Stop'
dotnet build .\EngineeringManager.sln --no-restore
dotnet test .\EngineeringManager.sln --no-build
```

预期：0 个构建错误，完整测试通过。

- [ ] **Step 4: 启动独立开发实例并浏览器验收**

使用未占用的新端口，不停止现有 `5075`-`5078` 实例。验证 `1440x900`、`1024x768`、`390x844`：公司切换、左右布局、所有列表弹窗、权属条件字段、附件上传/下载/替换/删除、保存后筛选保持、项目设备读取，以及无重叠、空白弹窗和桌面主要操作横向滚动。

- [ ] **Step 5: 最终复验与交付**

重新执行受影响测试，记录服务 URL、测试数量和剩余风险。按 Trellis 流程单独提出提交计划；未经用户确认不执行 `git add`、`git commit`、数据库更新或远程操作。
