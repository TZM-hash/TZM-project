using System.Security.Claims;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Projects.Records;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager + "," + SystemRoles.Finance)]
public sealed class EditModel(IProjectWorkspaceService workspaceService, IProjectConstructionService constructionService, IProjectRecordAttachmentService attachmentService, IFinanceLedgerService financeService, ApplicationDbContext db) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public ProjectConstructionWorkspaceDto Construction { get; private set; } = new([], [], [], []);
    public IReadOnlyDictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>> Attachments { get; private set; } = new Dictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>>();
    [BindProperty(SupportsGet = true)] public Guid ProjectId { get; set; }
    [BindProperty(SupportsGet = true)] public string Section { get; set; } = "collection";
    [BindProperty] public ProjectRecordAttachmentType RecordType { get; set; }
    [BindProperty] public Guid RecordId { get; set; }
    [BindProperty] public IFormFile? AttachmentFile { get; set; }
    [BindProperty] public string? AttachmentDescription { get; set; }
    [BindProperty] public FinanceEditInput FinanceEdit { get; set; } = new();
    [BindProperty] public ConstructionEditInput ConstructionEdit { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken token)
    {
        if (!IsKnownSection()) return BadRequest("项目明细分区无效。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        await LoadAsync(token);
        return Workspace is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAttachmentAsync(CancellationToken token)
    {
        if (!IsKnownSection() || !AttachmentTypeMatchesSection()) return BadRequest("附件类型与当前项目明细分区不匹配。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        if (AttachmentFile is null) return RedirectToPage(new { projectId = ProjectId, section = Section });
        await using var input = AttachmentFile.OpenReadStream();
        using var content = new MemoryStream();
        await input.CopyToAsync(content, token);
        await attachmentService.UploadAsync(Actor(), new ProjectRecordAttachmentUpload(ProjectId, RecordType, RecordId, AttachmentFile.FileName, AttachmentFile.ContentType, content.ToArray(), AttachmentDescription), token);
        return RedirectToPage(new { projectId = ProjectId, section = Section });
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid attachmentId, CancellationToken token)
    {
        if (!IsKnownSection()) return BadRequest("项目明细分区无效。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        var file = await attachmentService.DownloadAsync(ProjectId, attachmentId, token);
        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    public async Task<IActionResult> OnPostDeleteAttachmentAsync(Guid attachmentId, CancellationToken token)
    {
        if (!IsKnownSection()) return BadRequest("项目明细分区无效。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        await attachmentService.DeleteAsync(Actor(), ProjectId, attachmentId, token);
        return RedirectToPage(new { projectId = ProjectId, section = Section });
    }

    public async Task<IActionResult> OnPostFinanceAsync(CancellationToken token)
    {
        if (!FinanceKindMatchesSection()) return BadRequest("财务记录类型与当前项目明细分区不匹配。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        var recordScope = await CheckFinanceRecordScopeAsync(token);
        if (recordScope == FinanceRecordScope.Missing) return NotFound();
        if (recordScope == FinanceRecordScope.Shared) return BadRequest("该记录同时分摊到多个项目，请前往中央账本编辑整笔记录。");
        var actor = new FinanceRecordActor(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, User.Identity?.Name);
        var reason = string.IsNullOrWhiteSpace(FinanceEdit.Reason) ? "项目明细页详细编辑" : FinanceEdit.Reason.Trim();
        if (FinanceEdit.Amount <= 0m) throw new ArgumentException("金额必须大于零。");
        switch (FinanceEdit.Kind)
        {
            case FinanceEntryKind.Receivable:
                await financeService.UpdateReceivableAsync(actor, new UpdateReceivableRequest(FinanceEdit.Id, ProjectId, FinanceEdit.ContractId, FinanceEdit.LegalEntityId!.Value, FinanceEdit.BusinessPartnerId, FinanceEdit.EntryDate, FinanceEdit.DueDate, FinanceEdit.Amount, FinanceEdit.Description, FinanceEdit.ConcurrencyStamp, reason), token);
                break;
            case FinanceEntryKind.Collection:
                await financeService.UpdateCollectionAsync(actor, new UpdateCollectionRequest(FinanceEdit.Id, FinanceEdit.RelatedEntryId, ProjectId, FinanceEdit.ContractId, FinanceEdit.LegalEntityId!.Value, FinanceEdit.BusinessPartnerId, FinanceEdit.AccountId!.Value, FinanceEdit.EntryDate, FinanceEdit.Amount, FinanceEdit.PaymentMethod, FinanceEdit.Description, FinanceEdit.ConcurrencyStamp, reason), token);
                break;
            case FinanceEntryKind.Invoice:
                await financeService.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(FinanceEdit.Id, ProjectId, FinanceEdit.ContractId, FinanceEdit.LegalEntityId!.Value, FinanceEdit.BusinessPartnerId, InvoiceDirection.Output, FinanceEdit.InvoiceNumber ?? string.Empty, FinanceEdit.EntryDate, FinanceEdit.ProjectTaxConfigurationId!.Value, FinanceEdit.NetAmount, FinanceEdit.TaxAmount, FinanceEdit.Amount, FinanceEdit.InvoiceStatus, FinanceEdit.ConcurrencyStamp, reason), token);
                break;
            case FinanceEntryKind.Payable:
                await financeService.UpdatePayableAsync(actor, new UpdatePayableRequest(FinanceEdit.Id, ProjectId, FinanceEdit.ContractId, FinanceEdit.LegalEntityId!.Value, FinanceEdit.BusinessPartnerId!.Value, FinanceEdit.EntryDate, FinanceEdit.DueDate, FinanceEdit.Amount, FinanceEdit.Description, FinanceEdit.ConcurrencyStamp, reason), token);
                break;
            case FinanceEntryKind.Payment:
                await financeService.UpdatePaymentAsync(actor, new UpdatePaymentRequest(FinanceEdit.Id, FinanceEdit.RelatedEntryId, ProjectId, FinanceEdit.ContractId, FinanceEdit.LegalEntityId!.Value, FinanceEdit.BusinessPartnerId!.Value, FinanceEdit.AccountId!.Value, FinanceEdit.EntryDate, FinanceEdit.Amount, FinanceEdit.PaymentMethod, FinanceEdit.Description, FinanceEdit.ConcurrencyStamp, reason), token);
                break;
            default: throw new ArgumentException("不支持修改该财务记录。");
        }
        return RedirectToPage(new { projectId = ProjectId, section = Section });
    }

    public async Task<IActionResult> OnPostConstructionAsync(CancellationToken token)
    {
        if (!string.Equals(Section, "construction", StringComparison.OrdinalIgnoreCase)) return BadRequest("施工记录只能从施工详情分区编辑。");
        var accessResult = await CheckProjectAccessAsync(token);
        if (accessResult is not null) return accessResult;
        if (ConstructionEdit.Id.HasValue && !await db.ProjectConstructionRecords.AnyAsync(item => item.Id == ConstructionEdit.Id && item.ProjectId == ProjectId, token)) return NotFound();
        await constructionService.SaveAsync(new ProjectConstructionActor(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, User.Identity?.Name),
            new SaveProjectConstructionRecordRequest(ConstructionEdit.Id, ProjectId, ConstructionEdit.RecordType,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.Equipment ? ConstructionEdit.SubjectId : null,
                ConstructionEdit.RecordType == ProjectConstructionRecordType.ConstructionCrew ? ConstructionEdit.SubjectId : null,
                ConstructionEdit.TransferFromProjectId, ConstructionEdit.TransferToProjectId, ConstructionEdit.EntryDate, ConstructionEdit.ExitDate,
                ConstructionEdit.StopDays, ConstructionEdit.Notes, false, ConstructionEdit.ConcurrencyStamp,
                string.IsNullOrWhiteSpace(ConstructionEdit.Reason) ? "项目施工详情详细编辑" : ConstructionEdit.Reason.Trim(), ConstructionEdit.ShowInProjectOverview),
            DateOnly.FromDateTime(DateTime.Today), token);
        return RedirectToPage(new { projectId = ProjectId, section = Section });
    }

    private async Task LoadAsync(CancellationToken token)
    {
        Workspace = await workspaceService.GetAsync(ProjectId, token);
        if (Workspace is null) return;
        if (Section == "construction") Construction = await constructionService.GetWorkspaceAsync(ProjectId, DateOnly.FromDateTime(DateTime.Today), token);
        var targets = Section switch
        {
            "collection" => Workspace.Receivables.Select(item => (item.Id, ProjectRecordAttachmentType.Settlement)).Concat(Workspace.Collections.Select(item => (item.Id, ProjectRecordAttachmentType.Cash))),
            "invoice" => Workspace.Invoices.Select(item => (item.Id, ProjectRecordAttachmentType.Invoice)),
            "payment" => Workspace.Payables.Select(item => (item.Id, ProjectRecordAttachmentType.Settlement)).Concat(Workspace.Payments.Where(item => item.SourceType == "FinancePayment").Select(item => (item.Id, ProjectRecordAttachmentType.Cash))),
            "construction" => Construction.Records.Select(item => (item.Id, ProjectRecordAttachmentType.Construction)),
            _ => []
        };
        var result = new Dictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>>();
        foreach (var target in targets) result[target.Id] = await attachmentService.ListAsync(ProjectId, target.Item2, target.Id, token);
        Attachments = result;
    }

    private ProjectRecordAttachmentActor Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, true);

    private async Task<IActionResult?> CheckProjectAccessAsync(CancellationToken token)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(item => item.Id == ProjectId && item.IsActive, token)) return NotFound();
        if (User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance)) return null;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(SystemRoles.ProjectManager) && !string.IsNullOrWhiteSpace(userId) &&
            await db.Projects.AsNoTracking().AnyAsync(item => item.Id == ProjectId &&
                (item.ResponsibleUserId == userId || item.Assignments.Any(assignment => assignment.UserId == userId && assignment.IsActive)), token)) return null;
        return Forbid();
    }

    private bool IsKnownSection() => Section is not null &&
        (Section.Equals("collection", StringComparison.OrdinalIgnoreCase) ||
         Section.Equals("invoice", StringComparison.OrdinalIgnoreCase) ||
         Section.Equals("payment", StringComparison.OrdinalIgnoreCase) ||
         Section.Equals("construction", StringComparison.OrdinalIgnoreCase));

    private bool FinanceKindMatchesSection() =>
        string.Equals(Section, "collection", StringComparison.OrdinalIgnoreCase) && FinanceEdit.Kind is FinanceEntryKind.Receivable or FinanceEntryKind.Collection ||
        string.Equals(Section, "invoice", StringComparison.OrdinalIgnoreCase) && FinanceEdit.Kind == FinanceEntryKind.Invoice ||
        string.Equals(Section, "payment", StringComparison.OrdinalIgnoreCase) && FinanceEdit.Kind is FinanceEntryKind.Payable or FinanceEntryKind.Payment;

    private bool AttachmentTypeMatchesSection() =>
        string.Equals(Section, "collection", StringComparison.OrdinalIgnoreCase) && RecordType is ProjectRecordAttachmentType.Settlement or ProjectRecordAttachmentType.Cash ||
        string.Equals(Section, "invoice", StringComparison.OrdinalIgnoreCase) && RecordType == ProjectRecordAttachmentType.Invoice ||
        string.Equals(Section, "payment", StringComparison.OrdinalIgnoreCase) && RecordType is ProjectRecordAttachmentType.Settlement or ProjectRecordAttachmentType.Cash ||
        string.Equals(Section, "construction", StringComparison.OrdinalIgnoreCase) && RecordType == ProjectRecordAttachmentType.Construction;

    private async Task<FinanceRecordScope> CheckFinanceRecordScopeAsync(CancellationToken token)
    {
        if (FinanceEdit.Kind is FinanceEntryKind.Receivable or FinanceEntryKind.Payable)
        {
            var direction = FinanceEdit.Kind == FinanceEntryKind.Receivable ? LedgerDirection.Receivable : LedgerDirection.Payable;
            return await db.FinanceSettlements.AsNoTracking().AnyAsync(item => item.Id == FinanceEdit.Id && item.ProjectId == ProjectId && item.Direction == direction, token)
                ? FinanceRecordScope.CurrentProject
                : FinanceRecordScope.Missing;
        }

        if (FinanceEdit.Kind is FinanceEntryKind.Collection or FinanceEntryKind.Payment)
        {
            var cashType = FinanceEdit.Kind == FinanceEntryKind.Collection ? LedgerCashType.Collection : LedgerCashType.Payment;
            var projectIds = await db.FinanceCashEntries.AsNoTracking()
                .Where(item => item.Id == FinanceEdit.Id && item.CashType == cashType && !item.IsReversal)
                .SelectMany(item => item.Allocations.Select(allocation => allocation.ProjectId))
                .ToListAsync(token);
            if (projectIds.Count == 0 || !projectIds.Contains(ProjectId)) return FinanceRecordScope.Missing;
            return projectIds.Any(projectId => projectId != ProjectId) ? FinanceRecordScope.Shared : FinanceRecordScope.CurrentProject;
        }

        if (FinanceEdit.Kind == FinanceEntryKind.Invoice)
        {
            var projectIds = await db.FinanceInvoices.AsNoTracking()
                .Where(item => item.Id == FinanceEdit.Id && item.Direction == LedgerDirection.Receivable)
                .SelectMany(item => item.Allocations.Select(allocation => allocation.ProjectId))
                .ToListAsync(token);
            if (projectIds.Count == 0 || !projectIds.Contains(ProjectId)) return FinanceRecordScope.Missing;
            return projectIds.Any(projectId => projectId != ProjectId) ? FinanceRecordScope.Shared : FinanceRecordScope.CurrentProject;
        }

        return FinanceRecordScope.Missing;
    }

    private enum FinanceRecordScope
    {
        Missing,
        CurrentProject,
        Shared
    }

    public sealed class FinanceEditInput
    {
        public Guid Id { get; set; }
        public FinanceEntryKind Kind { get; set; }
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
        public string? InvoiceNumber { get; set; }
        public Guid? ProjectTaxConfigurationId { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public InvoiceStatus InvoiceStatus { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "项目明细页详细编辑";
    }

    public sealed class ConstructionEditInput
    {
        public Guid? Id { get; set; }
        public ProjectConstructionRecordType RecordType { get; set; }
        public Guid SubjectId { get; set; }
        public Guid? TransferFromProjectId { get; set; }
        public Guid? TransferToProjectId { get; set; }
        public DateOnly? EntryDate { get; set; }
        public DateOnly? ExitDate { get; set; }
        public int StopDays { get; set; }
        public string? Notes { get; set; }
        public bool ShowInProjectOverview { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "项目施工详情详细编辑";
    }
}
