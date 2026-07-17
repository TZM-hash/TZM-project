using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Companies.Certificates;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ICompanyCertificateService certificateService, ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyCertificateItemDto> Certificates { get; private set; } = [];
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<string> CertificateTypes { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty(SupportsGet = true)] public string? CertificateType { get; set; }
    [BindProperty(SupportsGet = true)] public CertificateExpiryState? State { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.ListAsync(actor, cancellationToken);
        Certificates = await certificateService.ListAsync(actor, new CertificateFilter(Search, CompanyId, CertificateType, State), Today(), cancellationToken);
        CertificateTypes = (await certificateService.ListAsync(actor, new CertificateFilter(), Today(), cancellationToken)).Select(item => item.CertificateType).Distinct().Order().ToArray();
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await certificateService.DownloadAttachmentAsync(await ResolveActorAsync(cancellationToken), id, cancellationToken);
        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid concurrencyStamp, CancellationToken cancellationToken)
    {
        await certificateService.DeleteAsync(await ResolveActorAsync(cancellationToken), id, concurrencyStamp, "管理员删除公司证书", cancellationToken);
        return RedirectToPage();
    }

    public static string StatusLabel(CertificateExpiryState state) => Employees.Certificates.IndexModel.StatusLabel(state);
    public static string StatusClass(CertificateExpiryState state) => Employees.Certificates.IndexModel.StatusClass(state);
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
}
