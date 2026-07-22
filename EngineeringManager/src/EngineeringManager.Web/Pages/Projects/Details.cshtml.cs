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
    IProjectWorkbookService projectWorkbookService,
    IProjectRecordAttachmentService attachmentService) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public ProjectEditOptionsDto Options { get; private set; } = new([], [], [], []);
    public FinanceEntryOptionsDto FinanceOptions { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public ProjectConstructionWorkspaceDto ConstructionWorkspace { get; private set; } = new([], [], [], []);
    public IReadOnlyDictionary<Guid, ProjectRecordAttachmentDto> QuantityAttachments { get; private set; } = new Dictionary<Guid, ProjectRecordAttachmentDto>();
    public IReadOnlyDictionary<Guid, ProjectRecordAttachmentDto> RecordAttachments { get; private set; } = new Dictionary<Guid, ProjectRecordAttachmentDto>();
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => CanManage || User.IsInRole(SystemRoles.Finance);
    public bool CanExportWorkbook => WorkbookActor().CanExport;
    public bool CanExportFullWorkbook => WorkbookActor().CanExportFullWorkbook;
    public bool CanExportWorkbookAttachments => WorkbookActor().CanExportAttachments;
    public string? ActiveInlineEditor { get; private set; }
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? RecordId { get; set; }
    [BindProperty] public QuickEditInput QuickEdit { get; set; } = new();
    [BindProperty] public List<QuantityEditInput> QuantityEdits { get; set; } = [];
    [BindProperty] public CreateQuantityInput CreateQuantity { get; set; } = new();
    [BindProperty] public IFormFile? QuantityAttachmentFile { get; set; }
    [BindProperty] public IFormFile? RecordAttachmentFile { get; set; }
    [BindProperty] public CollectionEditInput CollectionEdit { get; set; } = new();
    [BindProperty] public List<FinanceRowEditInput> CollectionRowEdits { get; set; } = [];
    [BindProperty] public List<FinanceRowEditInput> InvoiceRowEdits { get; set; } = [];
    [BindProperty] public List<FinanceRowEditInput> PayableRowEdits { get; set; } = [];
    [BindProperty] public List<FinanceRowEditInput> PaymentRowEdits { get; set; } = [];
    [BindProperty] public List<ConstructionEditInput> ConstructionRowEdits { get; set; } = [];
    [BindProperty] public InvoiceEditInput InvoiceEdit { get; set; } = new();
    [BindProperty] public PaymentEditInput PaymentEdit { get; set; } = new();
    [BindProperty] public FinanceRowEditInput FinanceRowEdit { get; set; } = new();
    [BindProperty] public ConstructionEditInput ConstructionEdit { get; set; } = new();
    [BindProperty] public ConstructionFlowInput ConstructionFlow { get; set; } = new();
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
                    SerializeGeneralContractors(QuickEdit.GeneralContractorNames, QuickEdit.GeneralContractorName),
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
                    ParseTaxConfigurations(QuickEdit.TaxConfigurationSelections),
                    QuickEdit.Contracts),
                cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            return await InlineErrorAsync(id, "project-overview", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostQuantitiesAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-quantity", cancellationToken);
        try
        {
            foreach (var quantityEdit in QuantityEdits.Where(item => item.IsDirty))
            {
                await projectService.UpdateLineItemAsync(new UpdateContractLineItemRequest(
                    Required(quantityEdit.LineItemId, "请选择工程量明细。"),
                    RequiredText(quantityEdit.Code, "请填写清单编码。"),
                    RequiredText(quantityEdit.Name, "请填写清单名称。"),
                    RequiredText(quantityEdit.Unit, "请填写单位。"),
                    null,
                    null,
                    null,
                    null,
                    false,
                    quantityEdit.ConcurrencyStamp,
                    quantityEdit.Notes,
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    "项目管理页面快捷修改工程量",
                    quantityEdit.Quantity,
                    quantityEdit.UnitPrice,
                    quantityEdit.AccountingLabel,
                    quantityEdit.RequiresInvoice), cancellationToken);
            }
            return RedirectToPage(new { id, tab = "quantity" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "quantity";
            return await InlineErrorAsync(id, "project-quantity", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCreateQuantityAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-quantity-create", cancellationToken);
        try
        {
            var workspace = await workspaceService.GetAsync(id, cancellationToken);
            if (workspace is null) return NotFound();
            var contractId = Required(CreateQuantity.ContractId, "请选择所属合同。");
            if (!workspace.Contracts.Any(contract => contract.Id == contractId)) throw new ArgumentException("所选合同不属于当前项目。");
            var lineItem = await projectService.AddLineItemAsync(new CreateContractLineItemRequest(
                contractId,
                RequiredText(CreateQuantity.Code, "请填写清单编码。"),
                RequiredText(CreateQuantity.Name, "请填写清单名称。"),
                RequiredText(CreateQuantity.Unit, "请填写单位。"),
                null,
                null,
                null,
                null,
                false,
                CreateQuantity.Notes,
                CreateQuantity.Quantity,
                CreateQuantity.UnitPrice,
                RequiredText(CreateQuantity.AccountingLabel, "请选择口径。"),
                CreateQuantity.RequiresInvoice), cancellationToken);
            if (QuantityAttachmentFile is not null)
            {
                try
                {
                    await attachmentService.ReplaceQuantityAsync(AttachmentActor(), await BuildQuantityUploadAsync(
                        id, lineItem.Id, QuantityAttachmentFile, cancellationToken), cancellationToken);
                }
                catch (Exception exception) when (IsEditableException(exception))
                {
                    TempData["Error"] = $"工程量已创建，但附件上传失败：{exception.Message}";
                }
            }
            return RedirectToQuantity(id, lineItem.Id);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "quantity";
            return await InlineErrorAsync(id, "project-quantity-create", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostQuantityAttachmentAsync(Guid id, Guid lineItemId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var file = QuantityAttachmentFile ?? throw new ArgumentException("请选择需要上传的附件。");
            await attachmentService.ReplaceQuantityAsync(AttachmentActor(), await BuildQuantityUploadAsync(
                id, lineItemId, file, cancellationToken), cancellationToken);
            return RedirectToQuantity(id, lineItemId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "quantity";
            return await InlineErrorAsync(id, "project-quantity", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnGetQuantityAttachmentAsync(Guid id, Guid attachmentId, bool download, CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.GetAsync(id, cancellationToken);
        if (workspace is null) return NotFound();
        var attachments = await LoadQuantityAttachmentsAsync(workspace, cancellationToken);
        if (!attachments.Values.Any(item => item.Id == attachmentId)) return NotFound();
        var file = await attachmentService.DownloadAsync(id, attachmentId, cancellationToken);
        return download
            ? File(file.Content, file.ContentType, file.OriginalFileName)
            : File(file.Content, file.ContentType);
    }

    public async Task<IActionResult> OnPostDeleteQuantityAttachmentAsync(Guid id, Guid attachmentId, Guid lineItemId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var workspace = await workspaceService.GetAsync(id, cancellationToken);
            if (workspace is null) return NotFound();
            var attachments = await LoadQuantityAttachmentsAsync(workspace, cancellationToken);
            if (!attachments.TryGetValue(lineItemId, out var attachment) || attachment.Id != attachmentId) return NotFound();
            await attachmentService.DeleteAsync(AttachmentActor(), id, attachmentId, cancellationToken);
            return RedirectToQuantity(id, lineItemId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "quantity";
            return await InlineErrorAsync(id, "project-quantity", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostRecordAttachmentAsync(
        Guid id,
        ProjectRecordAttachmentType recordType,
        Guid recordId,
        string tab,
        CancellationToken cancellationToken)
    {
        if (!CanManageAttachment(recordType)) return Forbid();
        try
        {
            var file = RecordAttachmentFile ?? throw new ArgumentException("请选择需要上传的附件。");
            var (workspace, construction) = await LoadAttachmentContextAsync(id, cancellationToken);
            if (!RecordBelongsToProject(workspace, construction, recordType, recordId)) return NotFound();
            await attachmentService.ReplaceAsync(AttachmentActor(recordType), await BuildRecordUploadAsync(
                id, recordType, recordId, file, cancellationToken), cancellationToken);
            return RedirectToRecord(id, tab, recordId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = tab;
            return await InlineErrorAsync(id, $"project-{tab}", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnGetRecordAttachmentAsync(Guid id, Guid attachmentId, bool download, CancellationToken cancellationToken)
    {
        var (workspace, construction) = await LoadAttachmentContextAsync(id, cancellationToken);
        var attachments = await LoadRecordAttachmentsAsync(workspace, construction, cancellationToken);
        if (!attachments.Values.Any(item => item.Id == attachmentId)) return NotFound();
        var file = await attachmentService.DownloadAsync(id, attachmentId, cancellationToken);
        return download
            ? File(file.Content, file.ContentType, file.OriginalFileName)
            : File(file.Content, file.ContentType);
    }

    public async Task<IActionResult> OnPostDeleteRecordAttachmentAsync(
        Guid id,
        ProjectRecordAttachmentType recordType,
        Guid recordId,
        Guid attachmentId,
        string tab,
        CancellationToken cancellationToken)
    {
        if (!CanManageAttachment(recordType)) return Forbid();
        try
        {
            var (workspace, construction) = await LoadAttachmentContextAsync(id, cancellationToken);
            if (!RecordBelongsToProject(workspace, construction, recordType, recordId)) return NotFound();
            var attachments = await attachmentService.ListAsync(id, recordType, recordId, cancellationToken);
            if (!attachments.Any(item => item.Id == attachmentId)) return NotFound();
            await attachmentService.DeleteAsync(AttachmentActor(recordType), id, attachmentId, cancellationToken);
            return RedirectToRecord(id, tab, recordId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = tab;
            return await InlineErrorAsync(id, $"project-{tab}", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCollectionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-collection-create", cancellationToken);
        try
        {
            var legalEntityId = Required(CollectionEdit.LegalEntityId, "请选择签约公司。");
            if (CollectionEdit.Kind != FinanceEntryKind.Collection) throw new ArgumentException("收款快捷编辑只支持登记收款。");
            var collectionId = await financeService.RecordCollectionAsync(new RecordCollectionRequest(
                null, id, CollectionEdit.ContractId, legalEntityId,
                CollectionEdit.BusinessPartnerId, Required(CollectionEdit.AccountId, "请选择收款账户。"),
                CollectionEdit.EntryDate, Positive(CollectionEdit.Amount), CollectionEdit.PaymentMethod,
                CollectionEdit.Description), cancellationToken);
            await TryAttachCreatedRecordAsync(id, ProjectRecordAttachmentType.Cash, collectionId, "collection", RecordAttachmentFile, cancellationToken);
            return RedirectToRecord(id, "collection", collectionId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "collection";
            return await InlineErrorAsync(id, "project-collection-create", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCollectionsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-collection", cancellationToken);
        try
        {
            var actor = new FinanceRecordActor(Actor().UserId, Actor().UserName);
            foreach (var collectionEdit in CollectionRowEdits.Where(item => item.IsDirty))
            {
                await financeService.UpdateCollectionAsync(actor, new UpdateCollectionRequest(
                    collectionEdit.Id, collectionEdit.RelatedEntryId, id, collectionEdit.ContractId,
                    Required(collectionEdit.LegalEntityId, "请选择签约公司。"), collectionEdit.BusinessPartnerId,
                    Required(collectionEdit.AccountId, "请选择收款账户。"), collectionEdit.EntryDate,
                    Positive(collectionEdit.Amount), collectionEdit.CollectionPaymentMethod,
                    collectionEdit.Description, collectionEdit.ConcurrencyStamp,
                    "项目管理页面快捷修改收款"), cancellationToken);
            }
            return RedirectToPage(new { id, tab = "collection" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "collection";
            return await InlineErrorAsync(id, "project-collection", exception, cancellationToken);
        }
    }


    public async Task<IActionResult> OnPostInvoicesAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-invoice", cancellationToken);
        try
        {
            var actor = new FinanceRecordActor(Actor().UserId, Actor().UserName);
            foreach (var invoiceEdit in InvoiceRowEdits.Where(item => item.IsDirty))
            {
                var taxConfigurationId = Required(invoiceEdit.ProjectTaxConfigurationId, "请选择税率和发票类型。");
                var amounts = await ResolveInvoiceAmountsAsync(id, invoiceEdit.Amount, taxConfigurationId, cancellationToken);
                await financeService.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(
                    invoiceEdit.Id, id, invoiceEdit.ContractId, Required(invoiceEdit.LegalEntityId, "请选择签约公司。"), invoiceEdit.BusinessPartnerId,
                    InvoiceDirection.Output, RequiredText(invoiceEdit.InvoiceNumber, "请填写发票号码。"), invoiceEdit.EntryDate,
                    taxConfigurationId, amounts.NetAmount, amounts.TaxAmount, amounts.GrossAmount, invoiceEdit.InvoiceStatus,
                    invoiceEdit.ConcurrencyStamp, "项目管理页面快捷修改开票", invoiceEdit.Description), cancellationToken);
            }
            return RedirectToPage(new { id, tab = "invoice" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "invoice";
            return await InlineErrorAsync(id, "project-invoice", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostPaymentsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-payment", cancellationToken);
        try
        {
            var actor = new FinanceRecordActor(Actor().UserId, Actor().UserName);
            foreach (var payableEdit in PayableRowEdits.Where(item => item.IsDirty))
            {
                await financeService.UpdatePayableAsync(actor, new UpdatePayableRequest(
                    payableEdit.Id, id, payableEdit.ContractId, Required(payableEdit.LegalEntityId, "请选择签约公司。"),
                    Required(payableEdit.BusinessPartnerId, "请选择收款单位。"), payableEdit.EntryDate, payableEdit.DueDate,
                    Positive(payableEdit.Amount), payableEdit.Description, payableEdit.ConcurrencyStamp,
                    "项目管理页面快捷修改应付"), cancellationToken);
            }
            foreach (var paymentEdit in PaymentRowEdits.Where(item => item.IsDirty))
            {
                if (string.Equals(paymentEdit.SourceType, "PayrollCrewDisbursement", StringComparison.Ordinal))
                    throw new InvalidOperationException("工资代发形成的付款记录由工资批次自动生成，不能在项目页快捷修改。");
                await financeService.UpdatePaymentAsync(actor, new UpdatePaymentRequest(
                    paymentEdit.Id, paymentEdit.RelatedEntryId, id, paymentEdit.ContractId,
                    Required(paymentEdit.LegalEntityId, "请选择签约公司。"), Required(paymentEdit.BusinessPartnerId, "请选择收款单位。"),
                    Required(paymentEdit.AccountId, "请选择付款账户。"), paymentEdit.EntryDate, Positive(paymentEdit.Amount),
                    RequiredText(paymentEdit.PaymentMethod, "请填写付款方式。"), paymentEdit.Description, paymentEdit.ConcurrencyStamp,
                    "项目管理页面快捷修改付款"), cancellationToken);
            }
            return RedirectToPage(new { id, tab = "payment" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "payment";
            return await InlineErrorAsync(id, "project-payment", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostConstructionsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-construction", cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            foreach (var constructionEdit in ConstructionRowEdits.Where(item => item.IsDirty))
            {
                await constructionService.SaveAsync(ConstructionActor(), new SaveProjectConstructionRecordRequest(
                    constructionEdit.Id, id, constructionEdit.RecordType,
                    constructionEdit.RecordType == ProjectConstructionRecordType.Equipment ? constructionEdit.SubjectId : null,
                    constructionEdit.RecordType == ProjectConstructionRecordType.ConstructionCrew ? constructionEdit.SubjectId : null,
                    null, null, constructionEdit.EntryDate, constructionEdit.ExitDate,
                    constructionEdit.StopDays, constructionEdit.Notes, false,
                    constructionEdit.ConcurrencyStamp == Guid.Empty ? null : constructionEdit.ConcurrencyStamp,
                    string.IsNullOrWhiteSpace(constructionEdit.Reason) ? "项目管理页面快捷修改施工详情" : constructionEdit.Reason,
                    constructionEdit.ShowInProjectOverview), today, cancellationToken);
            }
            return RedirectToPage(new { id, tab = "construction" });
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "construction";
            return await InlineErrorAsync(id, "project-construction", exception, cancellationToken);
        }
    }
    public async Task<IActionResult> OnPostInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-invoice-create", cancellationToken);
        try
        {
            var taxConfigurationId = Required(InvoiceEdit.ProjectTaxConfigurationId, "请选择税率和发票类型。");
            var amounts = await ResolveInvoiceAmountsAsync(id, InvoiceEdit.GrossAmount, taxConfigurationId, cancellationToken);
            var invoiceId = await financeService.AddInvoiceAsync(new CreateInvoiceRequest(
                id, InvoiceEdit.ContractId, Required(InvoiceEdit.LegalEntityId, "请选择签约公司。"), InvoiceEdit.BusinessPartnerId,
                InvoiceDirection.Output, RequiredText(InvoiceEdit.InvoiceNumber, "请填写发票号码。"), InvoiceEdit.InvoiceDate,
                taxConfigurationId, amounts.NetAmount, amounts.TaxAmount, amounts.GrossAmount,
                InvoiceStatus.IssuedOrReceived, [], [], InvoiceEdit.Description), cancellationToken);
            await TryAttachCreatedRecordAsync(id, ProjectRecordAttachmentType.Invoice, invoiceId, "invoice", RecordAttachmentFile, cancellationToken);
            return RedirectToRecord(id, "invoice", invoiceId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "invoice";
            return await InlineErrorAsync(id, "project-invoice-create", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManageFinance) return Forbid();
        if (!ModelState.IsValid) return await InlineValidationErrorAsync(id, "project-payment-create", cancellationToken);
        try
        {
            var legalEntityId = Required(PaymentEdit.LegalEntityId, "请选择签约公司。");
            var partnerId = Required(PaymentEdit.BusinessPartnerId, "请选择收款单位。");
            Guid recordId;
            ProjectRecordAttachmentType attachmentType;
            if (PaymentEdit.Kind == FinanceEntryKind.Payable)
            {
                recordId = await financeService.AddPayableAsync(new CreatePayableRequest(
                    id, PaymentEdit.ContractId, legalEntityId, partnerId, PayableSourceType.Manual,
                    PaymentEdit.EntryDate, PaymentEdit.DueDate, Positive(PaymentEdit.Amount), PaymentEdit.Description), cancellationToken);
                attachmentType = ProjectRecordAttachmentType.Settlement;
            }
            else if (PaymentEdit.Kind == FinanceEntryKind.Payment)
            {
                recordId = await financeService.RecordPaymentAsync(new RecordPaymentRequest(
                    PaymentEdit.RelatedEntryId, id, PaymentEdit.ContractId, legalEntityId, partnerId,
                    Required(PaymentEdit.AccountId, "请选择付款账户。"), PaymentEdit.EntryDate,
                    Positive(PaymentEdit.Amount), RequiredText(PaymentEdit.PaymentMethod, "请填写付款方式。"), PaymentEdit.Description), cancellationToken);
                attachmentType = ProjectRecordAttachmentType.Cash;
            }
            else throw new ArgumentException("付款快捷编辑只支持新增应付或登记付款。");
            await TryAttachCreatedRecordAsync(id, attachmentType, recordId, "payment", RecordAttachmentFile, cancellationToken);
            return RedirectToRecord(id, "payment", recordId);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "payment";
            return await InlineErrorAsync(id, "project-payment-create", exception, cancellationToken);
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
                    throw new InvalidOperationException("项目应收由工程量明细自动生成，不能手工修改。");
                case FinanceEntryKind.Collection:
                    await financeService.UpdateCollectionAsync(actor, new UpdateCollectionRequest(
                        FinanceRowEdit.Id, FinanceRowEdit.RelatedEntryId, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), FinanceRowEdit.BusinessPartnerId,
                        Required(FinanceRowEdit.AccountId, "请选择收款账户。"), FinanceRowEdit.EntryDate, Positive(FinanceRowEdit.Amount), FinanceRowEdit.CollectionPaymentMethod,
                        FinanceRowEdit.Description, FinanceRowEdit.ConcurrencyStamp, reason), cancellationToken);
                    break;
                case FinanceEntryKind.Invoice:
                    var taxConfigurationId = Required(FinanceRowEdit.ProjectTaxConfigurationId, "请选择税率和发票类型。");
                    var amounts = await ResolveInvoiceAmountsAsync(id, FinanceRowEdit.Amount, taxConfigurationId, cancellationToken);
                    await financeService.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(
                        FinanceRowEdit.Id, id, FinanceRowEdit.ContractId, Required(FinanceRowEdit.LegalEntityId, "请选择签约公司。"), FinanceRowEdit.BusinessPartnerId,
                        InvoiceDirection.Output, RequiredText(FinanceRowEdit.InvoiceNumber, "请填写发票号码。"), FinanceRowEdit.EntryDate,
                        taxConfigurationId, amounts.NetAmount, amounts.TaxAmount, amounts.GrossAmount, FinanceRowEdit.InvoiceStatus,
                        FinanceRowEdit.ConcurrencyStamp, reason, FinanceRowEdit.Description), cancellationToken);
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
                        Required(FinanceRowEdit.AccountId, "请选择付款账户。"), FinanceRowEdit.EntryDate, Positive(FinanceRowEdit.Amount), RequiredText(FinanceRowEdit.PaymentMethod, "请填写付款方式。"),
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
            var saved = await constructionService.SaveAsync(ConstructionActor(), new SaveProjectConstructionRecordRequest(
                ConstructionEdit.Id, id, ConstructionEdit.RecordType,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.Equipment ? ConstructionEdit.SubjectId : null,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.ConstructionCrew ? ConstructionEdit.SubjectId : null,
                null, null, ConstructionEdit.EntryDate, ConstructionEdit.ExitDate,
                ConstructionEdit.StopDays, ConstructionEdit.Notes, false,
                ConstructionEdit.ConcurrencyStamp == Guid.Empty ? null : ConstructionEdit.ConcurrencyStamp,
                RequiredText(ConstructionEdit.Reason, "请填写修改原因。"), ConstructionEdit.ShowInProjectOverview), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            await TryAttachCreatedRecordAsync(id, ProjectRecordAttachmentType.Construction, saved.Id, "construction", RecordAttachmentFile, cancellationToken);
            return RedirectToRecord(id, "construction", saved.Id);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            Tab = "construction";
            return await InlineErrorAsync(id, "project-construction-create", exception, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostConstructionFlowAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var workspace = await constructionService.GetWorkspaceAsync(id, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            if (!workspace.Records.Any(item => item.Id == ConstructionFlow.RecordId)) return NotFound();
            var actor = ConstructionActor();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var reason = RequiredText(ConstructionFlow.Reason, "请填写修改原因。");
            switch (ConstructionFlow.Action)
            {
                case "previous":
                    await constructionService.LinkPreviousAsync(actor, new LinkProjectConstructionRecordRequest(
                        ConstructionFlow.RecordId, Required(ConstructionFlow.TargetProjectId, "请选择需要连接的上一个项目。"),
                        ConstructionFlow.ConcurrencyStamp, reason), today, cancellationToken);
                    break;
                case "next":
                    await constructionService.LinkNextAsync(actor, new LinkProjectConstructionRecordRequest(
                        ConstructionFlow.RecordId, Required(ConstructionFlow.TargetProjectId, "请选择需要关联的后续项目。"),
                        ConstructionFlow.ConcurrencyStamp, reason, ConstructionFlow.TargetEntryDate), today, cancellationToken);
                    break;
                case "unlink":
                    await constructionService.UnlinkAsync(actor, new UnlinkProjectConstructionRecordRequest(
                        ConstructionFlow.RecordId, ConstructionFlow.ConcurrencyStamp, reason), today, cancellationToken);
                    break;
                default:
                    throw new ArgumentException("不支持该施工流转操作。");
            }
            return RedirectToRecord(id, "construction", ConstructionFlow.RecordId);
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
            return await InlineErrorAsync(id, "project-equipment-create", exception, cancellationToken);
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
            return await InlineErrorAsync(id, "project-crew-create", exception, cancellationToken);
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
        QuantityAttachments = await LoadQuantityAttachmentsAsync(Workspace, cancellationToken);
        ConstructionWorkspace = await constructionService.GetWorkspaceAsync(id, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        RecordAttachments = await LoadRecordAttachmentsAsync(Workspace, ConstructionWorkspace, cancellationToken);
        if (CanManage)
        {
            Options = await workspaceService.GetEditOptionsAsync(cancellationToken);
            if (populateInputs)
            {
                QuickEdit = QuickEditInput.From(Workspace.Overview);
                QuickEdit.Contracts = Workspace.Contracts
                    .OrderByDescending(contract => contract.ContractType == ContractType.MainContract)
                    .ThenBy(contract => contract.ContractNumber)
                    .Select(contract => new ProjectContractQuickEditInput(
                        contract.Id,
                        contract.Name,
                        contract.TotalAmount == 0m ? null : contract.TotalAmount,
                        contract.ConcurrencyStamp))
                    .ToList();
            }
        }
        if (CanManageFinance) FinanceOptions = await financeService.GetEntryOptionsAsync(cancellationToken);
        if (!populateInputs) return;

        var defaultContract = Workspace.Contracts.Count > 0 ? Workspace.Contracts[0] : null;
        var defaultContractId = defaultContract?.Id;
        CreateQuantity.ContractId = defaultContractId;
        var defaultLegalEntityId = Workspace.Overview.LegalEntities.Select(option => Guid.TryParse(option.Value, out var value) ? value : Guid.Empty).FirstOrDefault(value => value != Guid.Empty);
        CollectionEdit.ContractId = defaultContractId;
        CollectionEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
        InvoiceEdit.ContractId = defaultContractId;
        InvoiceEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
        InvoiceEdit.ProjectTaxConfigurationId = Workspace.Overview.TaxConfigurations?.FirstOrDefault(item => item.IsActive)?.Id;
        var contractorOptions = ProjectGeneralContractorOptions();
        if (contractorOptions.Length == 1 && contractorOptions[0].Id != Guid.Empty)
        {
            CollectionEdit.BusinessPartnerId = contractorOptions[0].Id;
        }
        if (contractorOptions.Length > 0 && contractorOptions[0].Id != Guid.Empty)
        {
            InvoiceEdit.BusinessPartnerId = contractorOptions[0].Id;
        }
        PaymentEdit.ContractId = defaultContractId;
        PaymentEdit.LegalEntityId = defaultLegalEntityId == Guid.Empty ? null : defaultLegalEntityId;
    }

    private ProjectWorkspaceActor Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.Identity?.Name);
    private ProjectRecordAttachmentActor AttachmentActor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, CanManage);
    private ProjectRecordAttachmentActor AttachmentActor(ProjectRecordAttachmentType recordType) =>
        new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, CanManageAttachment(recordType));
    private ProjectListActor WorkbookProjectActor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly) || User.IsInRole(SystemRoles.EquipmentManager);
        return new ProjectListActor(userId, canAccessAll);
    }
    private ProjectWorkbookActor WorkbookActor() =>
        new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.FindAll(ClaimTypes.Role).Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());
    private ProjectConstructionActor ConstructionActor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.Identity?.Name);
    private static bool IsEditableException(Exception exception) => exception is ArgumentException or InvalidOperationException or KeyNotFoundException or IOException or DbUpdateConcurrencyException;
    private static Guid Required(Guid? value, string message) => value is { } id && id != Guid.Empty ? id : throw new ArgumentException(message);
    private static string RequiredText(string? value, string message) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException(message);
    private static decimal Positive(decimal value) => value > 0 ? value : throw new ArgumentException("金额必须大于 0。");
    private RedirectResult RedirectToQuantity(Guid projectId, Guid lineItemId)
    {
        var pageUrl = Url.Page("/Projects/Details", new { id = projectId, tab = "quantity" }) ?? $"/Projects/Details/{projectId}?tab=quantity";
        return Redirect($"{pageUrl}#quantity-line-{lineItemId}");
    }

    private RedirectResult RedirectToRecord(Guid projectId, string tab, Guid recordId)
    {
        var safeTab = tab is "collection" or "invoice" or "payment" or "construction" ? tab : "quantity";
        var pageUrl = Url.Page("/Projects/Details", new { id = projectId, tab = safeTab }) ?? $"/Projects/Details/{projectId}?tab={safeTab}";
        return Redirect($"{pageUrl}#project-record-{recordId}");
    }

    private async Task<IReadOnlyDictionary<Guid, ProjectRecordAttachmentDto>> LoadQuantityAttachmentsAsync(ProjectWorkspaceDto workspace, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ProjectRecordAttachmentDto>();
        foreach (var lineItem in workspace.Contracts.SelectMany(contract => contract.LineItems))
        {
            var attachments = await attachmentService.ListAsync(workspace.Overview.Id, ProjectRecordAttachmentType.Quantity, lineItem.Id, cancellationToken);
            var attachment = attachments.Count > 0 ? attachments[0] : null;
            if (attachment is not null) result[lineItem.Id] = attachment;
        }
        return result;
    }

    private static async Task<ProjectRecordAttachmentUpload> BuildQuantityUploadAsync(Guid projectId, Guid lineItemId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > 20 * 1024 * 1024) throw new ArgumentException("附件不能为空且不能超过 20MB。");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new ProjectRecordAttachmentUpload(projectId, ProjectRecordAttachmentType.Quantity, lineItemId, file.FileName, file.ContentType, buffer.ToArray());
    }

    private async Task<(ProjectWorkspaceDto Workspace, ProjectConstructionWorkspaceDto Construction)> LoadAttachmentContextAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.GetAsync(projectId, cancellationToken) ?? throw new KeyNotFoundException("项目不存在。");
        var construction = await constructionService.GetWorkspaceAsync(projectId, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return (workspace, construction);
    }

    private async Task<IReadOnlyDictionary<Guid, ProjectRecordAttachmentDto>> LoadRecordAttachmentsAsync(
        ProjectWorkspaceDto workspace,
        ProjectConstructionWorkspaceDto construction,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ProjectRecordAttachmentDto>();
        async Task AddAsync(ProjectRecordAttachmentType type, Guid recordId)
        {
            var attachments = await attachmentService.ListAsync(workspace.Overview.Id, type, recordId, cancellationToken);
            if (attachments.Count > 0) result[recordId] = attachments[0];
        }

        foreach (var row in workspace.Collections) await AddAsync(ProjectRecordAttachmentType.Cash, row.Id);
        foreach (var row in workspace.Invoices) await AddAsync(ProjectRecordAttachmentType.Invoice, row.Id);
        foreach (var row in workspace.Payables) await AddAsync(ProjectRecordAttachmentType.Settlement, row.Id);
        foreach (var row in workspace.Payments) await AddAsync(ProjectRecordAttachmentType.Cash, row.Id);
        foreach (var row in construction.Records) await AddAsync(ProjectRecordAttachmentType.Construction, row.Id);
        return result;
    }

    private bool CanManageAttachment(ProjectRecordAttachmentType recordType) => recordType switch
    {
        ProjectRecordAttachmentType.Quantity or ProjectRecordAttachmentType.Construction => CanManage,
        ProjectRecordAttachmentType.Settlement or ProjectRecordAttachmentType.Invoice or ProjectRecordAttachmentType.Cash => CanManageFinance,
        _ => false
    };

    private static bool RecordBelongsToProject(
        ProjectWorkspaceDto workspace,
        ProjectConstructionWorkspaceDto construction,
        ProjectRecordAttachmentType recordType,
        Guid recordId) => recordType switch
    {
        ProjectRecordAttachmentType.Quantity => workspace.Contracts.SelectMany(item => item.LineItems).Any(item => item.Id == recordId),
        ProjectRecordAttachmentType.Settlement => workspace.Payables.Any(item => item.Id == recordId),
        ProjectRecordAttachmentType.Invoice => workspace.Invoices.Any(item => item.Id == recordId),
        ProjectRecordAttachmentType.Cash => workspace.Collections.Any(item => item.Id == recordId) || workspace.Payments.Any(item => item.Id == recordId),
        ProjectRecordAttachmentType.Construction => construction.Records.Any(item => item.Id == recordId),
        _ => false
    };

    private async Task TryAttachCreatedRecordAsync(
        Guid projectId,
        ProjectRecordAttachmentType recordType,
        Guid recordId,
        string tab,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) return;
        try
        {
            await attachmentService.ReplaceAsync(
                AttachmentActor(recordType),
                await BuildRecordUploadAsync(projectId, recordType, recordId, file, cancellationToken),
                cancellationToken);
        }
        catch (Exception exception) when (IsEditableException(exception))
        {
            var recordLabel = tab switch
            {
                "collection" => "收款记录",
                "invoice" => "开票记录",
                "payment" => "付款明细",
                "construction" => "施工记录",
                _ => "业务记录"
            };
            TempData["Error"] = $"{recordLabel}已创建，但附件上传失败：{exception.Message}";
        }
    }

    private static async Task<ProjectRecordAttachmentUpload> BuildRecordUploadAsync(
        Guid projectId,
        ProjectRecordAttachmentType recordType,
        Guid recordId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > 20 * 1024 * 1024) throw new ArgumentException("附件不能为空且不能超过 20MB。");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new ProjectRecordAttachmentUpload(projectId, recordType, recordId, file.FileName, file.ContentType, buffer.ToArray());
    }
    private static ProjectTaxConfigurationInput[] ParseTaxConfigurations(IEnumerable<string> selections) =>
        selections.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item =>
        {
            var parts = item.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var percent) ||
                !Enum.TryParse<ProjectInvoiceType>(parts[1], out var invoiceType))
                throw new ArgumentException("项目税金配置格式无效。");
            return new ProjectTaxConfigurationInput(percent / 100m, invoiceType);
        }).ToArray();


    private static string? SerializeGeneralContractors(IEnumerable<string>? names, string? fallbackName)
    {
        var values = (names ?? []).ToList();
        if (values.Count == 0 && !string.IsNullOrWhiteSpace(fallbackName))
        {
            values.Add(fallbackName);
        }

        return ProjectGeneralContractors.Serialize(values);
    }

    private FinanceOptionDto[] ProjectGeneralContractorOptions()
    {
        var names = ProjectGeneralContractors.Parse(Workspace?.Overview.GeneralContractorName);
        if (names.Count == 0)
        {
            return [];
        }

        var partners = FinanceOptions?.BusinessPartners ?? [];
        return names.Select(name =>
        {
            var match = partners.FirstOrDefault(item => string.Equals(item.Label, name, StringComparison.OrdinalIgnoreCase));
            return match ?? new FinanceOptionDto(Guid.Empty, name);
        }).ToArray();
    }

    private async Task<(decimal NetAmount, decimal TaxAmount, decimal GrossAmount)> ResolveInvoiceAmountsAsync(
        Guid projectId,
        decimal grossAmount,
        Guid? taxConfigurationId,
        CancellationToken cancellationToken)
    {
        var gross = Positive(grossAmount);
        Workspace ??= await workspaceService.GetAsync(projectId, cancellationToken)
            ?? throw new KeyNotFoundException("项目不存在。");
        var configuration = (Workspace.Overview.TaxConfigurations ?? [])
            .FirstOrDefault(item => item.Id == taxConfigurationId && item.IsActive)
            ?? throw new ArgumentException("请选择税率和发票类型。");
        var split = InvoiceAmountValidator.SplitGross(gross, configuration.TaxRate);
        return (split.NetAmount, split.TaxAmount, gross);
    }

    public sealed class QuickEditInput
    {
        public string ProjectNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentProjectName { get; set; }
        public string? GeneralContractorName { get; set; }
        public List<string> GeneralContractorNames { get; set; } = [];
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
        public List<ProjectContractQuickEditInput> Contracts { get; set; } = [];
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "快捷编辑项目资料";

        public static QuickEditInput From(ProjectWorkspaceOverviewDto item) => new()
        {
            ProjectNumber = item.ProjectNumber,
            Name = item.Name,
            ParentProjectName = item.ParentProjectName,
            GeneralContractorName = item.GeneralContractorName,
            GeneralContractorNames = ProjectGeneralContractors.Parse(item.GeneralContractorName).ToList(),
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
        public bool IsDirty { get; set; }
    }

    public sealed class CreateQuantityInput
    {
        public Guid? ContractId { get; set; }
        public string AccountingLabel { get; set; } = "暂估";
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? Notes { get; set; }
        public bool RequiresInvoice { get; set; } = true;
    }

    public sealed class CollectionEditInput
    {
        public FinanceEntryKind Kind { get; set; } = FinanceEntryKind.Collection;
        public Guid? ContractId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? RelatedEntryId { get; set; }
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? DueDate { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; } = "银行转账";
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
        public string? Description { get; set; }
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
        public string? PaymentMethod { get; set; } = "银行转账";
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
        public string? PaymentMethod { get; set; }
        public string? CollectionPaymentMethod { get; set; }
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
        public string SourceType { get; set; } = string.Empty;
        public bool IsDirty { get; set; }
    }

    public sealed class ConstructionEditInput
    {
        public Guid? Id { get; set; }
        public ProjectConstructionRecordType RecordType { get; set; } = ProjectConstructionRecordType.Equipment;
        public Guid? SubjectId { get; set; }
        public DateOnly? EntryDate { get; set; }
        public DateOnly? ExitDate { get; set; }
        public int StopDays { get; set; }
        public string? Notes { get; set; }
        public bool ShowInProjectOverview { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "项目管理页面快捷修改施工详情";
        public bool IsDirty { get; set; }
    }

    public sealed class ConstructionFlowInput
    {
        public Guid RecordId { get; set; }
        public Guid? TargetProjectId { get; set; }
        public DateOnly? TargetEntryDate { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = "项目管理页面调整施工流转";
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
