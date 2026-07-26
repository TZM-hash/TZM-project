using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Companies.Certificates;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(ICompanyCertificateService certificateService, ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyCertificateItemDto> Certificates { get; private set; } = [];
    public IReadOnlyList<CompanyCertificateItemDto> PortfolioCertificates { get; private set; } = [];
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<CompanyCertificateSummary> CompanySummaries { get; private set; } = [];
    public IReadOnlyList<string> CertificateTypes { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public string ScopeName => CompanyId.HasValue
        ? Companies.FirstOrDefault(item => item.Id == CompanyId.Value)?.ShortName ?? "全部公司"
        : "全部公司";
    public string? ActiveDialog { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty(SupportsGet = true)] public string? CertificateType { get; set; }
    [BindProperty(SupportsGet = true)] public CertificateExpiryState? State { get; set; }
    [BindProperty] public CertificateEditorInput Editor { get; set; } = new();
    [BindProperty] public IFormFile? AttachmentFile { get; set; }
    [BindProperty] public DeleteCertificateInput DeleteInput { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(DeleteInput));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            CertificateAttachmentUpload? upload = null;
            if (AttachmentFile is not null)
            {
                if (AttachmentFile.Length is <= 0 or > CertificateAttachmentUpload.MaxSizeBytes)
                    throw new InvalidOperationException("证书附件不能为空且不能超过 20MB。");
                await using var buffer = new MemoryStream();
                await AttachmentFile.CopyToAsync(buffer, cancellationToken);
                upload = new CertificateAttachmentUpload(AttachmentFile.FileName, AttachmentFile.ContentType, buffer.ToArray());
            }

            await certificateService.SaveAsync(await ResolveActorAsync(cancellationToken), Editor.ToRequest(upload), Today(), cancellationToken);
            TempData["SuccessMessage"] = Editor.Id.HasValue ? "公司证书已更新。" : "公司证书已新增。";
            return RedirectToPage(new { Search, CompanyId, CertificateType, State });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(Editor));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "delete";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var certificate = await certificateService.GetAsync(actor, DeleteInput.Id, Today(), cancellationToken);
            var expected = string.IsNullOrWhiteSpace(certificate.CertificateNumber) ? certificate.CertificateType : certificate.CertificateNumber;
            if (!string.Equals(DeleteInput.ConfirmationText.Trim(), expected, StringComparison.Ordinal))
                throw new InvalidOperationException("确认内容与证书编号或证书类型不一致。");

            await certificateService.DeleteAsync(actor, DeleteInput.Id, DeleteInput.ConcurrencyStamp, DeleteInput.Reason, cancellationToken);
            TempData["SuccessMessage"] = "公司证书已删除。";
            return RedirectToPage(new { Search, CompanyId, CertificateType, State });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "delete";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid id, bool download, CancellationToken cancellationToken)
    {
        try
        {
            var file = await certificateService.DownloadAttachmentAsync(await ResolveActorAsync(cancellationToken), id, cancellationToken);
            return download ? File(file.Content, file.ContentType, file.OriginalFileName) : File(file.Content, file.ContentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.ListAsync(actor, cancellationToken);
        if (CompanyId.HasValue && Companies.All(item => item.Id != CompanyId.Value)) CompanyId = null;

        var companyIds = Companies.Select(item => item.Id).ToHashSet();
        PortfolioCertificates = (await certificateService.ListAsync(actor, new CertificateFilter(), Today(), cancellationToken))
            .Where(item => companyIds.Contains(item.LegalEntityId))
            .ToArray();
        Certificates = (await certificateService.ListAsync(actor, new CertificateFilter(Search, CompanyId, CertificateType, State), Today(), cancellationToken))
            .Where(item => companyIds.Contains(item.LegalEntityId))
            .ToArray();
        CertificateTypes = PortfolioCertificates.Select(item => item.CertificateType).Distinct().Order().ToArray();
        CompanySummaries = Companies.Select(company =>
        {
            var items = PortfolioCertificates.Where(item => item.LegalEntityId == company.Id).ToArray();
            return new CompanyCertificateSummary(
                company.Id,
                company.ShortName,
                items.Length,
                items.Count(item => item.State is CertificateExpiryState.Normal or CertificateExpiryState.LongTerm),
                items.Count(item => item.State is CertificateExpiryState.Info or CertificateExpiryState.Warning or CertificateExpiryState.Critical),
                items.Count(item => item.State == CertificateExpiryState.Expired));
        }).ToArray();
    }

    private void RemoveModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => key.Equals(prefix, StringComparison.Ordinal) || key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }

    public static string StatusLabel(CertificateExpiryState state) => Employees.Certificates.IndexModel.StatusLabel(state);
    public static string StatusClass(CertificateExpiryState state) => Employees.Certificates.IndexModel.StatusClass(state);
    public static string AttachmentContentType(string? fileName) => Path.GetExtension(fileName)?.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    public sealed record CompanyCertificateSummary(Guid CompanyId, string CompanyName, int TotalCount, int ActiveCount, int ReminderCount, int ExpiredCount);

    public sealed class CertificateEditorInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "请选择所属公司。")]
        public Guid? LegalEntityId { get; set; }
        [Required(ErrorMessage = "请填写证书类型。"), StringLength(100)]
        public string CertificateType { get; set; } = string.Empty;
        [StringLength(100)] public string? CertificateNumber { get; set; }
        [StringLength(500)] public string? SpecialtyLevelScope { get; set; }
        [StringLength(200)] public string? IssuingAuthority { get; set; }
        public DateOnly? IssuedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        public string? ExistingAttachmentFileName { get; set; }
        public bool RemoveAttachment { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required(ErrorMessage = "请填写修改原因。")]
        public string Reason { get; set; } = "维护公司证书";

        public SaveCompanyCertificateItemRequest ToRequest(CertificateAttachmentUpload? upload) => new(
            Id,
            LegalEntityId!.Value,
            CertificateType,
            CertificateNumber,
            SpecialtyLevelScope,
            IssuingAuthority,
            IssuedOn,
            ExpiresOn,
            upload,
            RemoveAttachment,
            Notes,
            ConcurrencyStamp,
            Reason);
    }

    public sealed class DeleteCertificateInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string ExpectedText { get; set; } = string.Empty;
        [Required(ErrorMessage = "请输入证书编号或证书类型以确认删除。")]
        public string ConfirmationText { get; set; } = string.Empty;
        [Required(ErrorMessage = "请填写删除原因。")]
        public string Reason { get; set; } = "删除公司证书";
    }
}
