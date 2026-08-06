using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(
    ICompanyManagementService companyService,
    ICompanyCertificateService certificateService,
    ICompanyActorService actorService,
    IEmployeeService employeeService,
    IOrganizationSummaryService? organizationSummaryService = null)
    : CompanyPageModel(actorService)
{
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public CompanyDashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    public IReadOnlyList<CompanyCategoryDto> Categories { get; private set; } = [];
    public IReadOnlyDictionary<Guid, CompanyDetailsDto> CompanyDetails { get; private set; } = new Dictionary<Guid, CompanyDetailsDto>();
    public IReadOnlyDictionary<Guid, OrganizationSummaryDto> OrganizationSummaries { get; private set; } = new Dictionary<Guid, OrganizationSummaryDto>();
    public IReadOnlyList<CompanyCertificateItemDto> CompanyCertificates { get; private set; } = [];
    public int EmployeeCount { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public bool CategoryEditOpen { get; private set; }
    public bool CompanyDialogOpen { get; private set; }
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty] public CategoryInput Category { get; set; } = new();
    [BindProperty] public List<CategoryRowInput> CategoryRows { get; set; } = [];
    [BindProperty] public EditModel.InputModel CompanyInput { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCategoryAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveUnrelatedModelState($"{nameof(Category)}.");
        if (!TryValidateModel(Category, nameof(Category)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.SaveCategoryAsync(actor, new SaveCompanyCategoryRequest(null, Category.Code, Category.Name, Category.SortOrder, true, null, "维护公司组合分类"), cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCompanyAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveUnrelatedModelState($"{nameof(CompanyInput)}.");
        if (!TryValidateModel(CompanyInput, nameof(CompanyInput)))
        {
            CompanyDialogOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }
        try
        {
            await companyService.SaveCompanyAsync(await ResolveActorAsync(cancellationToken), CompanyInput.ToRequest(), cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            CompanyDialogOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCategoriesAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveUnrelatedModelState($"{nameof(CategoryRows)}[");
        if (CategoryRows.Count == 0 || !TryValidateModel(CategoryRows, nameof(CategoryRows)))
        {
            if (CategoryRows.Count == 0) ModelState.AddModelError(string.Empty, "没有可保存的公司组合分类。");
            CategoryEditOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var requests = CategoryRows.Select(category => new SaveCompanyCategoryRequest(category.Id, category.Code, category.Name,
                category.SortOrder, category.IsActive, category.ConcurrencyStamp, "批量修改公司组合分类")).ToList();
            await companyService.SaveCategoriesAsync(actor, requests, cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            CategoryEditOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(Guid categoryId, Guid concurrencyStamp, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.DeleteCategoryAsync(actor, categoryId, concurrencyStamp, cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            CategoryEditOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Companies = await companyService.SearchAsync(actor, Search, cancellationToken);
        var employees = await employeeService.ListAsync(null, false, cancellationToken);
        EmployeeCount = CountActiveEmployees(employees, Companies.Select(company => company.Id));
        Dashboard = await companyService.GetDashboardAsync(actor, CompanyId, cancellationToken);
        Categories = await companyService.ListCategoriesAsync(cancellationToken);
        var details = new Dictionary<Guid, CompanyDetailsDto>();
        foreach (var company in Companies)
        {
            details[company.Id] = await companyService.GetAsync(actor, company.Id, cancellationToken);
        }
        CompanyDetails = details;
        if (organizationSummaryService is not null)
        {
            var summaries = new Dictionary<Guid, OrganizationSummaryDto>();
            var asOf = DateOnly.FromDateTime(DateTime.Today);
            foreach (var company in Companies)
            {
                summaries[company.Id] = await organizationSummaryService.GetAsync(
                    new OrganizationSummaryQuery(OrganizationOwnerKind.LegalEntity, company.Id, asOf),
                    cancellationToken);
            }
            OrganizationSummaries = summaries;
        }
        CompanyCertificates = await certificateService.ListAsync(actor, new CertificateFilter(), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        if (CategoryRows.Count == 0)
        {
            CategoryRows = Categories.Select(CategoryRowInput.From).ToList();
        }
    }

    private void RemoveUnrelatedModelState(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            ModelState.Remove(key);
        }
    }

    public sealed class CategoryInput
    {
        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 50;
    }

    public sealed class CategoryRowInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
        [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        public static CategoryRowInput From(CompanyCategoryDto category) => new()
        {
            Id = category.Id,
            ConcurrencyStamp = category.ConcurrencyStamp,
            Code = category.Code,
            Name = category.Name,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        };
    }
}
