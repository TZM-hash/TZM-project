using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class EditModel(ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyCategoryDto> Categories { get; private set; } = [];
    [BindProperty] public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(Guid? id, Guid? copyFrom, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Categories = await companyService.ListCategoriesAsync(cancellationToken);
        if (copyFrom.HasValue) Input = InputModel.From(await companyService.PrepareCopyAsync(actor, copyFrom.Value, cancellationToken));
        else if (id.HasValue) Input = InputModel.From(await companyService.GetAsync(actor, id.Value, cancellationToken));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Categories = await companyService.ListCategoriesAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();
        var actor = await ResolveActorAsync(cancellationToken);
        var saved = await companyService.SaveCompanyAsync(actor, Input.ToRequest(), cancellationToken);
        return RedirectToPage("Details", new { id = saved.Id });
    }

    public sealed class InputModel
    {
        public Guid? Id { get; set; }
        [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [Required, StringLength(100)] public string ShortName { get; set; } = string.Empty;
        [Required] public Guid? CompanyCategoryId { get; set; }
        public string? LegalRepresentative { get; set; }
        public string? UnifiedSocialCreditCode { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? BusinessAddress { get; set; }
        public string? Phone { get; set; }
        public string? InvoiceTitle { get; set; }
        public string? Notes { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required] public string Reason { get; set; } = "维护公司档案";
        public SaveCompanyRequest ToRequest() => new(Id, Code, Name, ShortName, CompanyCategoryId, LegalRepresentative, UnifiedSocialCreditCode, RegisteredAddress, BusinessAddress, Phone, InvoiceTitle, Notes, ConcurrencyStamp, Reason);
        public static InputModel From(SaveCompanyRequest request) => new() { Id = request.Id, Code = request.Code, Name = request.Name, ShortName = request.ShortName, CompanyCategoryId = request.CompanyCategoryId, LegalRepresentative = request.LegalRepresentative, UnifiedSocialCreditCode = request.UnifiedSocialCreditCode, RegisteredAddress = request.RegisteredAddress, BusinessAddress = request.BusinessAddress, Phone = request.Phone, InvoiceTitle = request.InvoiceTitle, Notes = request.Notes, ConcurrencyStamp = request.ConcurrencyStamp, Reason = request.Reason };
        public static InputModel From(CompanyDetailsDto item) => new() { Id = item.Id, Code = item.Code, Name = item.Name, ShortName = item.ShortName, CompanyCategoryId = item.CompanyCategoryId, LegalRepresentative = item.LegalRepresentative, UnifiedSocialCreditCode = item.UnifiedSocialCreditCode, RegisteredAddress = item.RegisteredAddress, BusinessAddress = item.BusinessAddress, Phone = item.Phone, InvoiceTitle = item.InvoiceTitle, Notes = item.Notes, ConcurrencyStamp = item.ConcurrencyStamp, Reason = "修改公司档案" };
    }
}
