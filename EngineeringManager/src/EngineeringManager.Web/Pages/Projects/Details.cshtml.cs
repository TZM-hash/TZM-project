using System.Security.Claims;
using System.Globalization;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize]
public sealed class DetailsModel(
    IProjectWorkspaceService workspaceService,
    IProjectService projectService,
    IFinanceLedgerService financeService,
    IProjectConstructionService constructionService,
    IProjectWorkbookService projectWorkbookService) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public ProjectEditOptionsDto Options { get; private set; } = new([], [], [], []);
    public FinanceEntryOptionsDto FinanceOptions { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public ProjectConstructionWorkspaceDto ConstructionWorkspace { get; private set; } = new([], [], [], []);
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => CanManage || User.IsInRole(SystemRoles.Finance);
    public bool CanExportWorkbook => WorkbookActor().CanExport;
    public bool CanExportFullWorkbook => WorkbookActor().CanExportFullWorkbook;
    public bool CanExportWorkbookAttachments => WorkbookActor().CanExportAttachments;
    public string? ActiveInlineEditor { get; private set; }
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? RecordId { get; set; }
    [BindProperty] public QuickEditInput QuickEdit { get; set; } = new();
    [BindProperty] public QuantityEditInput QuantityEdit { get; set; } = new();
    [BindProperty] public CollectionEditInput CollectionEdit { get; set; } = new();
    [BindProperty] public InvoiceEditInput InvoiceEdit { get; set; } = new();
    [BindProperty] public PaymentEditInput PaymentEdit { get; set; } = new();
    [BindProperty] public FinanceRowEditInput FinanceRowEdit { get; set; } = new();
    [BindProperty] public ConstructionEditInput ConstructionEdit { get; set; } = new();
    [BindProperty] public NewEquipmentInput NewEquipment { get; set; } = new();
    [BindProperty] public NewCrewInput NewCrew { get; set; } = new();
    [BindProperty] public List<ProjectWorkbookSheet> SelectedWorkbookSheets { get; set; } = [];
    [BindProperty] public bool IncludeWorkbookAttachments { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (RecordId.HasValue) Tab = "construction";
        await LoadAsync(id, true, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostExportWorkbookAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanExportWorkbook) return Forbid();
        if (Workspace is null) await LoadAsync(id, false, cancellationToken);
        if (Workspace is null) return NotFound();
        var file = await projectWorkbookService.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(WorkbookProjectActor(), new ProjectListQuery(Workspace.Overview.ProjectNumber, [], null, null, null, null, null, false, IncludeInactive: true), false, [id]),
            SelectedWorkbookSheets,
            IncludeAttachments: IncludeWorkbookAttachments,
            Actor: WorkbookActor()), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> OnPostQuickEditAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-overview", cancellationToken);
        try
        {
            await workspaceService.UpdateAsync(
                Actor(),
                new UpdateProjectRequest(
                    id,
                    RequiredText(QuickEdit.ProjectNumber, "请填写项目编号。"),
                    RequiredText(QuickEdit.Name, "请填写项目名称。"),
                    QuickEdit.ParentProjectName,
                    QuickEdit.GeneralContractorName,
                    QuickEdit.GeneralContractorContact,
                    QuickEdit.GeneralContractorPhone,
                    QuickEdit.ResponsibleUserId,
                    QuickEdit.DepartmentId,
                    QuickEdit.BranchId,
                    QuickEdit.Stage,
                    QuickEdit.AffiliationType,
                    QuickEdit.LegalEntityIds,
                    QuickEdit.ConcurrencyStamp,
                    RequiredText(QuickEdit.Reason, "请填写修改原因。"),
                    QuickEdit.ActualStartDate,
                    QuickEdit.ActualCompletionDate,
                    QuickEdit.Notes,
                    QuickEdit.ContractSigningStatus,
                    ParseTaxConfigurations(QuickEdit.TaxConfigurationSelections)),
                cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            return await InlineErrorAsync(id, "project-overview", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostQuantityAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-quantity", cancellationToken);
        try
        {
            await projectService.UpdateLineItemAsync(new UpdateContractLineItemRequest(
                Required(QuantityEdit.LineItemId, "请选择工程量明细。"),
                RequiredText(QuantityEdit.Code, "请填写清单编码。"),
                RequiredText(QuantityEdit.Name, "请填写清单名称。"),
                RequiredText(QuantityEdit.Unit, "请填写单位。"),
                null,
                null,
                null,
                null,
                false,
                QuantityEdit.ConcurrencyStamp,
                QuantityEdit.Notes,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                "项目管理页面快捷修改工程量",
                QuantityEdit.Quantity,
                QuantityEdit.UnitPrice,
                QuantityEdit.AccountingLabel,
                QuantityEdit.RequiresInvoice), cancellationToken);
            return RedirectToPage(new { id, tab = "quantity" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "quantity";
            return await InlineErrorAsync(id, "project-quantity", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCollectionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-collection", cancellationToken);
        try
        {
            var legalEntityId = Required(CollectionEdit.LegalEntityId, "请选择签约公司。");
            if (CollectionEdit.Kind == FinanceEntryKind.Receivable)
            {
                await financeService.AddReceivableAsync(new CreateReceivableRequest(
                    id, CollectionEdit.ContractId, legalEntityId, CollectionEdit.BusinessPartnerId,
                    ReceivableSourceType.Manual, CollectionEdit.EntryDate, CollectionEdit.DueDate,
                    Positive(CollectionEdit.Amount), CollectionEdit.Description), cancellationToken);
            }
            else if (CollectionEdit.Kind == FinanceEntryKind.Collection)
            {
                await financeService.RecordCollectionAsync(new RecordCollectionRequest(
                    CollectionEdit.RelatedEntryId, id, CollectionEdit.ContractId, legalEntityId,
                    CollectionEdit.BusinessPartnerId, Required(CollectionEdit.AccountId, "请选择收款账户。"),
                    CollectionEdit.EntryDate, Positive(CollectionEdit.Amount), CollectionEdit.PaymentMethod,
                    CollectionEdit.Description), cancellationToken);
            }
            else throw new ArgumentException("收款快捷编辑只支持新增应收或登记收款。");
            return RedirectToPage(new { id, tab = "collection" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "collection";
            return await InlineErrorAsync(id, "project-collection", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-invoice", cancellationToken);
        try
        {
            await financeService.AddInvoiceAsync(new CreateInvoiceRequest(
                id, InvoiceEdit.ContractId, Required(InvoiceEdit.LegalEntityId, "请选择签约公司。"), InvoiceEdit.BusinessPartnerId,
                InvoiceDirection.Output, RequiredText(InvoiceEdit.InvoiceNumber, "请填写发票号码。"), InvoiceEdit.InvoiceDate,
                Required(InvoiceEdit.ProjectTaxConfigurationId, "请选择税率和发票类型。"), InvoiceEdit.NetAmount, InvoiceEdit.TaxAmount, Positive(InvoiceEdit.GrossAmount),
                InvoiceStatus.IssuedOrReceived, [], []), cancellationToken);
            return RedirectToPage(new { id, tab = "invoice" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "invoice";
            return await InlineErrorAsync(id, "project-invoice", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-payment", cancellationToken);
        try
        {
            var legalEntityId = Required(PaymentEdit.LegalEntityId, "请选择签约公司。");
            var partnerId = Required(PaymentEdit.BusinessPartnerId, "请选择收款单位。");
            if (PaymentEdit.Kind == FinanceEntryKind.Payable)
            {
                await financeService.AddPayableAsync(new CreatePayableRequest(
                    id, PaymentEdit.ContractId, legalEntityId, partnerId, PayableSourceType.Manual,
                    PaymentEdit.EntryDate, PaymentEdit.DueDate, Positive(PaymentEdit.Amount), PaymentEdit.Description), cancellationToken);
            }
            else if (PaymentEdit.Kind == FinanceEntryKind.Payment)
            {
                await financeService.RecordPaymentAsync(new RecordPaymentRequest(
                    PaymentEdit.RelatedEntryId, id, PaymentEdit.ContractId, legalEntityId, partnerId,
                    Required(PaymentEdit.AccountId, "请选择付款账户。"), PaymentEdit.EntryDate,
                    Positive(PaymentEdit.Amount), PaymentEdit.PaymentMethod, PaymentEdit.Description), cancellationToken);
            }
            else throw new ArgumentException("付款快捷编辑只支持新增应付或登记付款。");
            return RedirectToPage(new { id, tab = "payment" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "payment";
            return await InlineErrorAsync(id, "project-payment", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostFinanceRowAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-" + FinanceRowEdit.Tab, cancellationToken);
        try
        {
            var actor = new FinanceRecordActor(Actor().UserId, Actor().UserName);
            var reason = RequiredText(FinanceRowEdit.Reason, "请填写修改原因。");
            switch (FinanceRowEdit.Kind)
            {
                case FinanceEntryKind.Receivable:
                    await financeService.UpdateReceivableAsync(actor, new UpdateReceivableRequest(
                        FinanceRowEdit.Id, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), FinanceRowEdit.BusinessPartnerId,
                        FinanceRowEdit.EntryDate, FinanceRowEdit.DueDate, Positive(FinanceRowEdit.Amount), FinanceRowEdit.Description,
                        FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                case FinanceEntryKind.Collection:
                    await financeService.UpdateCollectionAsync(actor, new UpdateCollectionRequest(
                        FinanceRowEdit.Id, FinanceRowEdit.RelatedEntryId, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), FinanceRowEdit.BusinessPartnerId,
                        Required(FinanceRowEdit.AccountId, "请选择收款账户。"), FinanceRowEdit.EntryDate, Positive(FinanceRowEdit.Amount), FinanceRowEdit.PaymentMethod,
                        FinanceRowEdit.Description, FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                case FinanceEntryKind.Invoice:
                    await financeService.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(
                        FinanceRowEdit.Id, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), FinanceRowEdit.BusinessPartnerId,
                        InvoiceDirection.Output, RequiredText(FinanceRowEdit.InvoiceNumber, "请填写发票号码。"), FinanceRowEdit.EntryDate,
                        Required(FinanceRowEdit.ProjectTaxConfigurationId, "请选择税率和发票类型。"), FinanceRowEdit.NetAmount, FinanceRowEdit.TaxAmount, Positive(FinanceRowEdit.Amount), FinanceRowEdit.InvoiceStatus,
                        FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                case FinanceEntryKind.Payable:
                    await financeService.UpdatePayableAsync(actor, new UpdatePayableRequest(
                        FinanceRowEdit.Id, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), Required(FinanceRowEdit.BusinessPartnerId, "请选择收款单位。"),
                        FinanceRowEdit.EntryDate, FinanceRowEdit.DueDate, Positive(FinanceRowEdit.Amount), FinanceRowEdit.Description,
                        FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                case FinanceEntryKind.Payment:
                    await financeService.UpdatePaymentAsync(actor, new UpdatePaymentRequest(
                        FinanceRowEdit.Id, FinanceRowEdit.RelatedEntryId, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), Required(FinanceRowEdit.BusinessPartnerId, "请选择收款单位。"),
                        Required(FinanceRowEdit.AccountId, "请选择付款账户。"), FinanceRowEdit.EntryDate, Positive(FinanceRowEdit.Amount), FinanceRowEdit.PaymentMethod,
                        FinanceRowEdit.Description, FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                default:
                    throw new ArgumentException("不支持修改该财务记录。");
            }
            return RedirectToPage(new { id, tab = FinanceRowEdit.Tab });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = FinanceRowEdit.Tab;
            return await InlineErrorAsync(id, "project-" + FinanceRowEdit.Tab, exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostConstructionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            await constructionService.SaveAsync(ConstructionActor(), new SaveProjectConstructionRecordRequest(
                ConstructionEdit.Id, id, ConstructionEdit.RecordType,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.Equipment ? ConstructionEdit.SubjectId : null,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.ConstructionCrew ? ConstructionEdit.SubjectId : null,
                ConstructionEdit.TransferFromProjectId, ConstructionEdit.TransferToProjectId, ConstructionEdit.EntryDate, ConstructionEdit.ExitDate,
                ConstructionEdit.StopDays, ConstructionEdit.Notes, ConstructionEdit.AutoConnectPrevious,
                ConstructionEdit.ConcurrencyStamp == Guid.Empty ? null : ConstructionEdit.ConcurrencyStamp,
                RequiredText(ConstructionEdit.Reason, "请填写修改原因。"), ConstructionEdit.ShowInProjectOverview), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "construction" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "construction";
            return await InlineErrorAsync(id, "project-construction", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCreateEquipmentAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var option = await constructionService.CreateEquipmentAsync(ConstructionActor(), new CreateProjectEquipmentRequest(
                RequiredText(NewEquipment.EquipmentNumber, "请填写设备编号。"), RequiredText(NewEquipment.Name, "请填写设备名称。"), NewEquipment.Model, NewEquipment.Category,
                NewEquipment.OwnershipType, NewEquipment.OwnerLegalEntityId, NewEquipment.LessorBusinessPartnerId, NewEquipment.InternalDailyRate,
                RequiredText(NewEquipment.Reason, "请填写创建原因。")), cancellationToken);
            await constructionService.SaveAsync(ConstructionActor(), new SaveProjectConstructionRecordRequest(null, id, ProjectConstructionRecordType.Equipment, option.Id, null, null, null, null, null, 0, null, false, null, "项目内新建设备施工记录"), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "construction" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "construction";
            return await InlineErrorAsync(id, "project-construction", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCreateCrewAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var option = await constructionService.CreateCrewAsync(ConstructionActor(), new CreateProjectCrewRequest(
                RequiredText(NewCrew.PartnerNumber, "请填写班组编号。"), RequiredText(NewCrew.Name, "请填写班组名称。"), NewCrew.ContactName, NewCrew.ContactPhone,
                RequiredText(NewCrew.Reason, "请填写创建原因。")), cancellationToken);
            await constructionService.SaveAsync(ConstructionActor(), new SaveProjectConstructionRecordRequest(null, id, ProjectConstructionRecordType.ConstructionCrew, null, option.Id, null, null, null, null, 0, null, false, null, "项目内新建班组施工记录"), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "construction" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "construction";
            return await InlineErrorAsync(id, "project-construction", exception, cancellationToken);
        }
    }

    private async Task<IActionResult> InlineErrorAsync(Guid id, string editor, Exception exception, CancellationToken cancellationToken)
    {
        ModelState.AddModelError(string.Empty, exception.Message);
        ActiveInlineEditor = editor;
        await LoadAsync(id, false, cancellationToken);
        return Page();
    }

    private async Task<IActionResult> InlineValidationErrorAsync(Guid id, string editor, CancellationToken cancellationToken)
    {
        ActiveInlineEditor = editor;
        await LoadAsync(id, false, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(Guid id, bool populateInputs, CancellationToken cancellationToken)
    {
        Workspace = await workspaceService.GetAsync(id, cancellationToken);
        if (Workspace is null) return;
        ConstructionWorkspace = await constructionService.GetWorkspaceAsync(id, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        if (CanManage)
        {
            Options = await workspaceService.GetEditOptionsAsync(cancellationToken);
            if (populateInputs) QuickEdit = QuickEditInput.From(Workspace.Overview);
        }
        if (CanManageFinance) FinanceOptions = await financeService.GetEntryOptionsAsync(cancellationToken);
        if (!populateInputs) return;

        var defaultContractId = Workspace.Contracts.Count > 0 ? Workspace.Contracts[0].Id : (Guid?)null;
        var defaultLegalEntityId = Workspace.Overview.LegalEntities.Select(option => Guid.TryParse(option.Value, out var value) ? value : Guid.Empty).FirstOrDefault(value => value != Guid.Empty);
        CollectionEdit.ContractId = defaultContractId;
        CollectionEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
        InvoiceEdit.ContractId = defaultContractId;
        InvoiceEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
        InvoiceEdit.ProjectTaxConfigurationId = Workspace.Overview.TaxConfigurations?.FirstOrDefault(item => item.IsActive)?.Id;
        PaymentEdit.ContractId = defaultContractId;
        PaymentEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
    }

    private ProjectWorkspaceActor Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.Identity?.Name);
    private ProjectListActor WorkbookProjectActor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly) || User.IsInRole(SystemRoles.EquipmentManager);
        return new ProjectListActor(userId, canAccessAll);
    }
    private ProjectWorkbookActor WorkbookActor() =>
        new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.FindAll(ClaimTypes.Role).Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());
    private ProjectConstructionActor ConstructionActor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.Identity?.Name);
    private static bool IsEditableException(Exception exception) => exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException;
    private static Guid Required(Guid? value, string message) => value is { } id && id != Guid.Empty ? id : throw new ArgumentException(message);
    private static string RequiredText(string? value, string message) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException(message);
    private static decimal Positive(decimal value) => value > 0 ? value : throw new ArgumentException("金额必须大于 0。");
    private static ProjectTaxConfigurationInput[] ParseTaxConfigurations(IEnumerable<string> selections) =>
        selections.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item =>
        {
            var parts = item.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var percent) ||
                !Enum.TryParse<ProjectInvoiceType>(parts[1], out var invoiceType))
                throw new ArgumentException("项目税金配置格式无效。");
            return new ProjectTaxConfigurationInput(percent / 100m, invoiceType);
        }).ToArray();

    public sealed class QuickEditInput
    {
        public string ProjectNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentProjectName { get; set; }
        public string? GeneralContractorName { get; set; }
        public string? GeneralContractorContact { get; set; }
        public string? GeneralContractorPhone { get; set; }
        public string? ResponsibleUserId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? BranchId { get; set; }
        public ProjectStage Stage { get; set; }
        public ContractSigningStatus ContractSigningStatus { get; set; }
        public ProjectAffiliationType AffiliationType { get; set; }
        public DateOnly? ActualStartDate { get; set; }
        public DateOnly? ActualCompletionDate { get; set; }
        public string? Notes { get; set; }
        public List<Guid> LegalEntityIds { get; set; } = [];
        public List<string> TaxConfigurationSelections { get; set; } = [];
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "快捷编辑项目资料";

        public static QuickEditInput From(ProjectWorkspaceOverviewDto item) => new()
        {
            ProjectNumber = item.ProjectNumber,
            Name = item.Name,
            ParentProjectName = item.ParentProjectName,
            GeneralContractorName = item.GeneralContractorName,
            GeneralContractorContact = item.GeneralContractorContact,
            GeneralContractorPhone = item.GeneralContractorPhone,
            ResponsibleUserId = item.ResponsibleUserId,
            DepartmentId = item.DepartmentId,
            BranchId = item.BranchId,
            Stage = item.Stage,
            ContractSigningStatus = item.ContractSigningStatus,
            AffiliationType = item.AffiliationType,
            ActualStartDate = item.ActualStartDate,
            ActualCompletionDate = item.ActualCompletionDate,
            Notes = item.Notes,
            LegalEntityIds = item.LegalEntities.Select(option => Guid.Parse(option.Value)).ToList(),
            TaxConfigurationSelections = item.TaxConfigurations?.Where(configuration => configuration.IsActive)
                .Select(configuration => $"{configuration.TaxRate * 100m:0}|{(int)configuration.InvoiceType}").ToList() ?? [],
            ConcurrencyStamp = item.ConcurrencyStamp
        };
    }

    public sealed class QuantityEditInput
    {
        public Guid? LineItemId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? AccountingLabel { get; set; }
        public bool RequiresInvoice { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class CollectionEditInput
    {
        public FinanceEntryKind Kind { get; set; } = FinanceEntryKind.Receivable;
        public Guid? ContractId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? RelatedEntryId { get; set; }
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? DueDate { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
        public string? Description { get; set; }
    }

    public sealed class InvoiceEditInput
    {
        public Guid? ContractId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public InvoiceDirection Direction { get; set; } = InvoiceDirection.Output;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public Guid? ProjectTaxConfigurationId { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrossAmount { get; set; }
    }

    public sealed class PaymentEditInput
    {
        public FinanceEntryKind Kind { get; set; } = FinanceEntryKind.Payable;
        public Guid? ContractId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? RelatedEntryId { get; set; }
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? DueDate { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
        public string? Description { get; set; }
    }

    public sealed class FinanceRowEditInput
    {
        public Guid Id { get; set; }
        public FinanceEntryKind Kind { get; set; }
        public string Tab { get; set; } = string.Empty;
        public Guid? ContractId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? RelatedEntryId { get; set; }
        public DateOnly EntryDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? Description { get; set; }
        public InvoiceDirection Direction { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? InvoiceType { get; set; }
        public Guid? ProjectTaxConfigurationId { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public InvoiceStatus InvoiceStatus { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "项目管理页面快捷修改";
    }

    public sealed class ConstructionEditInput
    {
        public Guid? Id { get; set; }
        public ProjectConstructionRecordType RecordType { get; set; } = ProjectConstructionRecordType.Equipment;
        public Guid? SubjectId { get; set; }
        public Guid? TransferFromProjectId { get; set; }
        public Guid? TransferToProjectId { get; set; }
        public DateOnly? EntryDate { get; set; }
        public DateOnly? ExitDate { get; set; }
        public int StopDays { get; set; }
        public string? Notes { get; set; }
        public bool AutoConnectPrevious { get; set; }
        public bool ShowInProjectOverview { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "项目管理页面快捷修改施工详情";
    }

    public sealed class NewEquipmentInput
    {
        public string EquipmentNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Category { get; set; }
        public EngineeringManager.Domain.Equipment.EquipmentOwnershipType OwnershipType { get; set; } = EngineeringManager.Domain.Equipment.EquipmentOwnershipType.SelfOwned;
        public Guid? OwnerLegalEntityId { get; set; }
        public Guid? LessorBusinessPartnerId { get; set; }
        public decimal? InternalDailyRate { get; set; }
        public string Reason { get; set; } = "项目内新建设备";
    }

    public sealed class NewCrewInput
    {
        public string PartnerNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string Reason { get; set; } = "项目内新建施工班组";
    }
}
