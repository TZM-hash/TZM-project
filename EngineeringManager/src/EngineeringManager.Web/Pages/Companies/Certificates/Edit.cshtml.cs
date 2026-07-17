using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Companies.Certificates;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class EditModel(ICompanyCertificateService certificateService, ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task OnGetAsync(Guid? id, Guid? companyId, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.ListAsync(actor, cancellationToken);
        if (id.HasValue) Input = InputModel.From(await certificateService.GetAsync(actor, id.Value, DateOnly.FromDateTime(DateTime.Today), cancellationToken));
        else if (companyId.HasValue) Input.LegalEntityId = companyId.Value;
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.ListAsync(actor, cancellationToken);
        if (!ModelState.IsValid) return Page();
        CertificateAttachmentUpload? upload = null;
        if (Input.Attachment is { Length: > 0 })
        {
            await using var memory = new MemoryStream();
            await Input.Attachment.CopyToAsync(memory, cancellationToken);
            upload = new CertificateAttachmentUpload(Input.Attachment.FileName, Input.Attachment.ContentType, memory.ToArray());
        }
        await certificateService.SaveAsync(actor, Input.ToRequest(upload), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return RedirectToPage("Index");
    }
    public sealed class InputModel
    {
        public Guid? Id { get; set; }
        [Required] public Guid LegalEntityId { get; set; }
        [Required, StringLength(100)] public string CertificateType { get; set; } = string.Empty;
        [StringLength(100)] public string? CertificateNumber { get; set; }
        [StringLength(500)] public string? SpecialtyLevelScope { get; set; }
        [StringLength(200)] public string? IssuingAuthority { get; set; }
        public DateOnly? IssuedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        public IFormFile? Attachment { get; set; }
        public string? ExistingAttachmentFileName { get; set; }
        public bool RemoveAttachment { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required] public string Reason { get; set; } = "维护公司证书";
        public SaveCompanyCertificateItemRequest ToRequest(CertificateAttachmentUpload? upload) => new(Id, LegalEntityId, CertificateType, CertificateNumber, SpecialtyLevelScope, IssuingAuthority, IssuedOn, ExpiresOn, upload, RemoveAttachment, Notes, ConcurrencyStamp, Reason);
        public static InputModel From(CompanyCertificateItemDto item) => new() { Id = item.Id, LegalEntityId = item.LegalEntityId, CertificateType = item.CertificateType, CertificateNumber = item.CertificateNumber, SpecialtyLevelScope = item.SpecialtyLevelScope, IssuingAuthority = item.IssuingAuthority, IssuedOn = item.IssuedOn, ExpiresOn = item.ExpiresOn, ExistingAttachmentFileName = item.AttachmentFileName, Notes = item.Notes, ConcurrencyStamp = item.ConcurrencyStamp, Reason = "修改公司证书" };
    }
}
