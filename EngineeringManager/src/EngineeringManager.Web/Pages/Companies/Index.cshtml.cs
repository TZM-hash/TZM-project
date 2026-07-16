using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ICompanyManagementService companyService, ICompanyActorService actorService)
    : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public CompanyDashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    public IReadOnlyList<CompanyCategoryDto> Categories { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty] public CategoryInput Category { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.ListAsync(actor, cancellationToken);
        Dashboard = await companyService.GetDashboardAsync(actor, CompanyId, cancellationToken);
        Categories = await companyService.ListCategoriesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCategoryAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        await companyService.SaveCategoryAsync(actor, new SaveCompanyCategoryRequest(null, Category.Code, Category.Name, Category.SortOrder, true, null, "维护公司组合分类"), cancellationToken);
        return RedirectToPage();
    }

    public sealed class CategoryInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 50;
    }
}
