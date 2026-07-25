# 自有公司新增弹窗与原位快捷编辑 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将自有公司的证照与账户新增改为弹窗，并让组合分类、基本信息、证书和账户统一采用原位置整组快捷编辑。

**Architecture:** 继续使用 Razor Pages 与现有 `quick-edit.js`。新增弹窗由页面内原生 `dialog` 和小型初始化代码控制；证书批量修改通过 `ICompanyCertificateService.SaveManyAsync` 一次校验、一次保存，账户继续复用现有批量服务。

**Tech Stack:** ASP.NET Core Razor Pages、EF Core、原生 JavaScript、CSS、xUnit、FluentAssertions

---

### Task 1: 用页面测试锁定目标结构

**Files:**
- Modify: `tests/EngineeringManager.Tests/Web/CompanyPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] **Step 1: 写证书与账户弹窗的失败测试**

断言 `Companies/Details.cshtml` 包含 `data-company-certificate-create-dialog`、`data-company-account-create-dialog`、顶部新增按钮和批量证书表单；同时断言不再包含证书操作列表头、行级证书编辑按钮、底部“新增公司证照/账户”面板。

- [ ] **Step 2: 写原位定位与三等分样式的失败测试**

断言 `pages.css` 包含：

```css
.company-category-create-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
.company-workspace--details .inline-edit-shell [data-inline-edit-control].inline-cell-control:not([hidden]),
.company-category-panel [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static; }
```

- [ ] **Step 3: 运行测试确认失败**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyPageTests|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ResponsiveUiAssetTests" --no-restore
```

预期：新增弹窗、证书批量编辑和静态定位断言失败。

### Task 2: 增加证书原子批量保存

**Files:**
- Modify: `src/EngineeringManager.Application/Certificates/ICompanyCertificateService.cs`
- Modify: `src/EngineeringManager.Infrastructure/Certificates/CompanyCertificateService.cs`
- Modify: `tests/EngineeringManager.Tests/Application/CompanyManagementServiceTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/CompanyPageTests.cs`

- [ ] **Step 1: 写批量保存失败测试**

创建同一公司的两条证书，调用：

```csharp
await service.SaveManyAsync(actor, requests, today, CancellationToken.None);
```

断言两条记录均更新并分别写入审计；再使用一条错误并发标记，断言抛出 `DbUpdateConcurrencyException` 且两条记录都未改变。

- [ ] **Step 2: 运行服务测试确认失败**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyManagementServiceTests" --no-restore
```

预期：`SaveManyAsync` 不存在导致编译失败。

- [ ] **Step 3: 扩展服务契约**

在 `ICompanyCertificateService` 增加：

```csharp
Task<IReadOnlyList<CompanyCertificateItemDto>> SaveManyAsync(
    CompanyActor actor,
    IReadOnlyList<SaveCompanyCertificateItemRequest> requests,
    DateOnly today,
    CancellationToken cancellationToken);
```

- [ ] **Step 4: 实现一次校验、一次保存**

实现必须先校验管理权限、非空请求、全部为已有证书、公司访问权、日期、原因和并发标记；全部通过后才更新字段、并为每条改变写审计，最后只调用一次 `SaveChangesAsync`。批量编辑不接受新增附件或删除附件请求。

- [ ] **Step 5: 运行服务测试确认通过**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyManagementServiceTests" --no-restore
```

预期：通过。

### Task 3: 重构证书页为新增弹窗和整表快捷编辑

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Companies/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Companies/Details.cshtml`
- Modify: `tests/EngineeringManager.Tests/Web/CompanyPageTests.cs`

- [ ] **Step 1: 写页面模型失败测试**

测试新增证照把 `CertificateAttachmentFile` 转成 `CertificateAttachmentUpload` 并传给 `SaveAsync`；验证失败时 `CertificateCreateOpen` 为真。测试 `OnPostCertificatesAsync` 将所有 `CertificateRows` 传给 `SaveManyAsync`，失败时 `CertificateEditOpen` 为真。

- [ ] **Step 2: 实现页面模型**

新增：

```csharp
public bool CertificateCreateOpen { get; private set; }
public bool CertificateEditOpen { get; private set; }
[BindProperty] public List<CertificateRowInput> CertificateRows { get; set; } = [];
```

单条 `OnPostCertificateAsync` 只负责新增，并把可选附件放进保存请求；批量处理器负责已有证书字段。`LoadAsync` 仅在没有已提交行数据时从 `Certificates` 填充 `CertificateRows`。

- [ ] **Step 3: 重写证书 Razor 结构**

面板标题按钮顺序为“新增证照”“快捷编辑”；表格字段保持资料类型、资料编号、签发日期、有效期、到期状态、备注、附件，删除操作列。顶部 `certificate-batch-form` 提交所有行。新增表单迁入带 `enctype="multipart/form-data"` 的 `dialog`。

- [ ] **Step 4: 运行证书页面测试**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyPageTests" --no-restore
```

预期：通过。

### Task 4: 重构账户新增弹窗并统一组合分类布局

**Files:**
- Modify: `src/EngineeringManager.Web/Pages/Companies/Details.cshtml.cs`
- Modify: `src/EngineeringManager.Web/Pages/Companies/Details.cshtml`
- Modify: `src/EngineeringManager.Web/Pages/Companies/Index.cshtml`
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `tests/EngineeringManager.Tests/Web/CompanyPageTests.cs`

- [ ] **Step 1: 补账户失败回显测试**

验证 `OnPostAccountAsync` 校验或服务失败时 `AccountCreateOpen` 为真；账户表为空时页面仍显示“新增账户”。

- [ ] **Step 2: 实现账户弹窗状态**

新增：

```csharp
public bool AccountCreateOpen { get; private set; }
```

把原底部账户表单迁入 `dialog`，按钮固定在快捷编辑左侧；新账户默认启用。

- [ ] **Step 3: 调整组合分类新增区**

把 `company-category-create-grid` 改为三等分，并统一 label、input 的高度、边框、圆角、背景和 `:focus-visible` 状态；窄屏继续折叠为单列。

- [ ] **Step 4: 运行页面测试**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyPageTests" --no-restore
```

预期：通过。

### Task 5: 修复四组原位编辑布局并验证

**Files:**
- Modify: `src/EngineeringManager.Web/wwwroot/css/pages.css`
- Modify: `tests/EngineeringManager.Tests/Web/InlineEditingPageTests.cs`
- Modify: `tests/EngineeringManager.Tests/Web/ResponsiveUiAssetTests.cs`

- [ ] **Step 1: 增加页面级静态定位覆盖**

只对自有公司详情和组合分类编辑器覆盖通用绝对定位：

```css
.company-workspace--details .inline-edit-shell [data-inline-edit-control].inline-cell-control:not([hidden]),
.company-category-panel [data-inline-edit-control].inline-cell-control:not([hidden]) {
  position: static;
  z-index: auto;
  inset: auto;
  width: 100%;
}
```

表格单元格、详情 `dd` 和按钮区保持稳定尺寸，备注输入允许正常换行。

- [ ] **Step 2: 运行自有公司定向测试**

```powershell
dotnet test tests/EngineeringManager.Tests/EngineeringManager.Tests.csproj --filter "FullyQualifiedName~CompanyPageTests|FullyQualifiedName~InlineEditingPageTests|FullyQualifiedName~ResponsiveUiAssetTests|FullyQualifiedName~CompanyManagementServiceTests" --no-restore
```

预期：全部通过。

- [ ] **Step 3: 构建 Web 项目**

```powershell
dotnet build src/EngineeringManager.Web/EngineeringManager.Web.csproj --no-restore
```

预期：0 个错误。

- [ ] **Step 4: 检查工作区差异**

确认项目管理页面无改动；确认只包含本设计文档、计划文档及自有公司相关应用、测试和样式修改。根据项目规则，不自动提交 git。
