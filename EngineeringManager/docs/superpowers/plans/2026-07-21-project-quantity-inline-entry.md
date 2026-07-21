# Project Quantity Inline Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在项目详情页完成工程量新增与单附件上传/替换，并删除独立工程量编辑页及全部入口。

**Architecture:** 通用附件上传继续允许多附件；应用服务新增仅面向工程量的替换方法，在一次数据库保存中软删除旧有效附件并加入新附件。项目详情 Razor Page 负责创建工程量、调用工程量附件替换、下载和删除，页面加载时建立工程量附件字典。

**Tech Stack:** ASP.NET Core Razor Pages、EF Core、xUnit、FluentAssertions、原生 JavaScript/CSS。

---

### Task 1: 工程量单附件替换服务

**Files:**
- Modify: `src/EngineeringManager.Application/Projects/IProjectRecordAttachmentService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Projects/ProjectRecordAttachmentService.cs`
- Modify: `tests/EngineeringManager.Tests/Application/ProjectRecordAttachmentServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectRecordEditPageModelTests.cs`

- [ ] **Step 1: 写失败测试**

在 `ProjectRecordAttachmentServiceTests` 中拆分并新增以下行为测试：

```csharp
[Fact]
public async Task ReplaceQuantityAsyncKeepsOnlyNewestAttachment()

[Fact]
public async Task ReplaceQuantityAsyncKeepsExistingAttachmentWhenStorageFails()

[Fact]
public async Task UploadAsyncStillAllowsMultipleAttachmentsForOtherRecordTypes()
```

第一项先用 `UploadAsync` 建立旧附件，再调用期望存在的 `ReplaceQuantityAsync`，断言列表只有新附件，并直接查询数据库断言旧记录 `IsDeleted == true`。第二项使用抛出异常的 `IFileStore`，断言旧附件仍有效。第三项继续证明施工等其他类型的 `UploadAsync` 未被收紧。

- [ ] **Step 2: 运行测试并确认因缺少新接口而失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter FullyQualifiedName~ProjectRecordAttachmentServiceTests
```

Expected: 编译失败，提示 `ReplaceQuantityAsync` 不存在。

- [ ] **Step 3: 实现最小替换能力**

接口新增：

```csharp
Task<ProjectRecordAttachmentDto> ReplaceQuantityAsync(
    ProjectRecordAttachmentActor actor,
    ProjectRecordAttachmentUpload upload,
    CancellationToken token);
```

实现必须：复用 20MB、文件名、权限和归属校验；拒绝非 `Quantity` 类型；先保存新物理文件；随后查询同项目同工程量的全部有效附件，统一设置 `IsDeleted = true`，加入新附件并只调用一次 `SaveChangesAsync`；任何数据库失败都删除新物理文件，旧数据库记录保持有效。`UploadAsync` 的多附件语义不变。

- [ ] **Step 4: 更新接口测试替身并运行目标测试**

所有实现 `IProjectRecordAttachmentService` 的测试替身加入抛出 `NotSupportedException` 的 `ReplaceQuantityAsync`。再次运行 Task 1 命令，Expected: PASS。

### Task 2: 项目详情页工程量新增与附件处理

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Projects/Details.cshtml`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`
- Create: `tests/EngineeringManager.Tests/Web/ProjectQuantityInlineEntryPageTests.cs`

- [ ] **Step 1: 写页面结构和授权失败测试**

页面源码测试断言：

```csharp
page.Should().Contain("<summary>新增工程量明细</summary>")
    .And.Contain("asp-page-handler=\"CreateQuantity\"")
    .And.Contain("asp-page-handler=\"QuantityAttachment\"")
    .And.Contain("<th>上传</th><th>附件</th><th class=\"quantity-notes-column\">备注</th>")
    .And.NotContain("/Projects/Contracts/Edit");
```

集成页面测试让 `ProjectManager` 看到新增区、上传列、附件预览/删除控件；让只读角色看不到创建和上传入口。测试工厂注册一个 `IProjectRecordAttachmentService` 替身，返回固定附件列表。

- [ ] **Step 2: 运行 Web 目标测试并确认失败**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectQuantityInlineEntryPageTests|FullyQualifiedName~ProjectAuthorizationTests"
```

Expected: 新增区、handler、上传列或附件替身相关断言失败。

- [ ] **Step 3: 扩展页面模型**

`DetailsModel` 注入 `IProjectRecordAttachmentService`，增加：

```csharp
public IReadOnlyDictionary<Guid, ProjectRecordAttachmentDto> QuantityAttachments { get; private set; }
[BindProperty] public CreateQuantityInput CreateQuantity { get; set; } = new();
[BindProperty] public IFormFile? QuantityAttachmentFile { get; set; }
[BindProperty] public string? QuantityAttachmentDescription { get; set; }
```

新增 `OnPostCreateQuantityAsync`：校验管理权限、合同属于当前项目、调用 `AddLineItemAsync`，可选调用 `ReplaceQuantityAsync`，成功重定向到 `tab=quantity` 和 `quantity-line-{id}` 锚点；失败时保留输入、打开新增折叠区并返回页面。

新增 `OnPostQuantityAttachmentAsync`、`OnGetQuantityAttachmentAsync`、`OnPostDeleteQuantityAttachmentAsync`。上传调用工程量专用替换方法；下载和删除复用现有服务；所有修改操作检查 `CanManage`，服务层继续检查项目与明细归属。

`LoadAsync` 为每条工程量调用 `ListAsync`，只把最新有效附件放入字典；初始化新增表单默认合同、口径“暂估”和需要开票。

- [ ] **Step 4: 改造详情页工程量区域**

在快捷编辑按钮下复用 `details.inline-create-details`，放置合同、口径、附件、附件说明、编号、名称、单位、工程量、单价、小计预览、备注和是否开票字段。工程量表格顺序固定为：编号、名称、单位、工程量、单价、小计、口径、是否开票、上传、附件、备注、快捷编辑操作。

每行上传使用独立 `multipart/form-data` 表单；附件列显示一个当前有效附件的预览链接与删除按钮，或“暂无附件”。移除工程量名称、附件和标题区域中所有 `/Projects/Contracts/Edit` 链接。

- [ ] **Step 5: 运行 Web 目标测试**

重复 Task 2 Step 2 命令，Expected: PASS。

### Task 3: 布局、旧路由清理与总体验证

**Files:**
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Reuse: `src/EngineeringManager.Web/wwwroot/js/components/attachment-picker.js`
- Delete: `src/EngineeringManager.Web/Pages/Projects/Contracts/Edit.cshtml`
- Delete: `src/EngineeringManager.Web/Pages/Projects/Contracts/Edit.cshtml.cs`
- Delete: `tests/EngineeringManager.Tests/Web/ContractEditPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ProjectAuthorizationTests.cs`

- [ ] **Step 1: 写旧路由和布局失败测试**

新增认证用户访问 `/Projects/Contracts/Edit` 返回 404 的测试；源码测试断言旧页面文件不存在。CSS 测试断言 `.quantity-inline-table` 使用紧凑/受控列宽、单元格左对齐，`.quantity-notes-column`/备注单元格获得剩余宽度并允许自然换行，小屏幕仍由 `.table-wrap` 横向滚动。

- [ ] **Step 2: 运行测试并确认旧页面仍导致失败**

运行 Task 2 的 Web 目标测试，Expected: 旧路由仍存在或文件仍存在，测试失败。

- [ ] **Step 3: 完成样式和脚本接入并删除旧页面**

将已存在的紧凑录入样式迁移到详情页新增区域，补充表格列类和备注换行规则。详情页加载 `attachment-picker.js`，并用小型模块脚本计算工程量 × 单价预览。删除旧 Razor Page、PageModel 和仅服务于该页面的测试。

- [ ] **Step 4: 运行定向测试、完整测试和构建**

```powershell
$ErrorActionPreference = 'Stop'
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj --filter "FullyQualifiedName~ProjectRecordAttachmentServiceTests|FullyQualifiedName~ProjectQuantityInlineEntryPageTests|FullyQualifiedName~ProjectAuthorizationTests"
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 test .\EngineeringManager.sln
& .\.tools\pwsh\pwsh.exe -NoLogo -NoProfile -File .\scripts\dotnet.ps1 build .\EngineeringManager.sln
```

Expected: 全部 PASS，build 0 errors。

- [ ] **Step 5: 页面验证**

启动本地应用，使用项目经理账号检查桌面和窄屏：新增折叠区、保存后锚点、首次上传、替换、删除、附件预览、备注列宽和横向滚动。确认旧 URL 返回 404。
