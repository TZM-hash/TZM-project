using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.EquipmentManager)]
public sealed class EditModel(
    IEquipmentService service,
    ICompanyManagementService companyService,
    IBusinessPartnerService partnerService) : EquipmentPageModel
{
    [BindProperty] public EquipmentEditorInput Input { get; set; } = new();
    [BindProperty] public IFormFile? QualificationAttachmentFile { get; set; }
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Lessors { get; private set; } = [];

    public async Task OnGetAsync(Guid? id, Guid? copyFrom, CancellationToken token)
    {
        if (id.HasValue)
            Input = EquipmentEditorInput.From(await service.GetEquipmentAsync(ResolveActor(), id.Value, token));
        else if (copyFrom.HasValue)
            Input = EquipmentEditorInput.From(await service.GetEquipmentAsync(ResolveActor(), copyFrom.Value, token), true);

        await LoadOptionsAsync(token);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(token);
            return Page();
        }

        try
        {
            CertificateAttachmentUpload? upload = null;
            if (QualificationAttachmentFile is not null)
            {
                await using var buffer = new MemoryStream();
                await QualificationAttachmentFile.CopyToAsync(buffer, token);
                upload = new CertificateAttachmentUpload(
                    QualificationAttachmentFile.FileName,
                    QualificationAttachmentFile.ContentType,
                    buffer.ToArray());
            }

            await service.SaveEquipmentAsync(ResolveActor(), Input.ToRequest(upload), token);
            return RedirectToPage("Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadOptionsAsync(token);
            return Page();
        }
    }

    private async Task LoadOptionsAsync(CancellationToken token)
    {
        var actor = ResolveActor();
        Companies = (await companyService.ListAsync(new CompanyActor(actor.UserId, false, actor.CanAccessAll, actor.AccessibleCompanyIds), token))
            .Where(item => item.IsActive)
            .ToArray();
        Lessors = await partnerService.ListAsync(null, null, token);
    }
}
