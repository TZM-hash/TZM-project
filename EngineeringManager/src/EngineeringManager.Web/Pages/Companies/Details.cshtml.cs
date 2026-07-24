using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public CompanyDetailsDto Company { get; private set; } = null!;
    public CompanyDashboardDto Dashboard { get; private set; } = null!;
    public IReadOnlyList<CompanyCategoryDto> Categories { get; private set; } = [];
    public IReadOnlyList<CompanyListItemDto> CompanyOptions { get; private set; } = [];
    public CompanyWorkspaceSummaryDto? WorkspaceSummary { get; private set; }
    public IReadOnlyList<CompanyActivityItemDto> RecentActivity { get; private set; } = [];
    public IReadOnlyList<CompanyProjectRowDto> Projects { get; private set; } = [];
    public IReadOnlyList<CompanyContractRowDto> Contracts { get; private set; } = [];
    public IReadOnlyList<CompanyCollectionRowDto> Collections { get; private set; } = [];
    public IReadOnlyList<CompanyPaymentRowDto> Payments { get; private set; } = [];
    public IReadOnlyList<CompanyInvoiceRowDto> Invoices { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public bool QuickEditOpen { get; private set; }
    public string ActiveTab => NormalizeTab(Tab);

    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? ProjectSearch { get; set; }
    [BindProperty] public AccountInput Account { get; set; } = new();
    [BindProperty] public CertificateInput Certificate { get; set; } = new();
    [BindProperty] public EditModel.InputModel QuickEdit { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostQuickEditAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "profile";
        QuickEdit.Id = id;
        if (!ModelState.IsValid)
        {
            QuickEditOpen = true;
            await LoadAsync(id, false, cancellationToken);
            return Page();
        }
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.SaveCompanyAsync(actor, QuickEdit.ToRequest(), cancellationToken);
            return RedirectToPage(new { id, tab = "profile" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            QuickEditOpen = true;
            await LoadAsync(id, false, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "accounts";
        ModelState.Clear();
        if (!TryValidateModel(Account, nameof(Account)))
        {
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.SaveAccountAsync(actor, new SaveCompanyAccountRequest(Account.Id, id, Account.Name, Account.Number, Account.BankName,
                Account.AccountType, Account.OpeningBalance, Account.DefaultCollection, Account.DefaultPayment, Account.DefaultInvoice, Account.IsActive,
                Account.ConcurrencyStamp, Account.Id.HasValue ? "修改公司账户" : "新增公司账户", Account.Notes), cancellationToken);
            return RedirectToPage(new { id, tab = "accounts" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAccountStatusAsync(Guid id, Guid accountId, Guid concurrencyStamp, bool isActive, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "accounts";
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var company = await companyService.GetAsync(actor, id, cancellationToken);
            var account = company.Accounts.SingleOrDefault(item => item.Id == accountId)
                ?? throw new KeyNotFoundException("公司账户不存在。");
            await companyService.SaveAccountAsync(actor, new SaveCompanyAccountRequest(account.Id, id, account.AccountName, account.AccountNumber,
                account.BankName, (int)Enum.Parse<FinancialAccountType>(account.AccountType), account.OpeningBalance,
                isActive && account.IsDefaultCollection, isActive && account.IsDefaultPayment, isActive && account.IsDefaultInvoice,
                isActive, concurrencyStamp, isActive ? "启用公司账户" : "停用公司账户", account.Notes), cancellationToken);
            return RedirectToPage(new { id, tab = "accounts" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCertificateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "certificates";
        var actor = await ResolveActorAsync(cancellationToken);
        await companyService.SaveCertificateAsync(actor, new SaveCompanyCertificateRequest(null, id, Certificate.Type, Certificate.Number,
            Certificate.IssuedOn, Certificate.ExpiresOn, null, Certificate.Notes, null, "维护公司证照"), cancellationToken);
        return RedirectToPage(new { id, tab = "certificates" });
    }

    private async Task LoadAsync(Guid id, bool populateQuickEdit, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Company = await companyService.GetAsync(actor, id, cancellationToken);
        Dashboard = await companyService.GetDashboardAsync(actor, id, cancellationToken);
        CompanyOptions = await companyService.ListAsync(actor, cancellationToken);

        if (CanManage)
        {
            Categories = await companyService.ListCategoriesAsync(cancellationToken);
            if (populateQuickEdit) QuickEdit = EditModel.InputModel.From(Company);
        }

        switch (ActiveTab)
        {
            case "overview":
                WorkspaceSummary = await companyService.GetWorkspaceSummaryAsync(actor, id, cancellationToken);
                RecentActivity = await companyService.ListRecentActivityAsync(actor, id, 10, cancellationToken);
                break;
            case "projects":
                Projects = await companyService.ListCompanyProjectsAsync(actor, id, ProjectSearch, 50, cancellationToken);
                Contracts = await companyService.ListCompanyContractsAsync(actor, id, null, 50, cancellationToken);
                break;
            case "finance":
                Collections = await companyService.ListCompanyCollectionsAsync(actor, id, 50, cancellationToken);
                Payments = await companyService.ListCompanyPaymentsAsync(actor, id, 50, cancellationToken);
                Invoices = await companyService.ListCompanyInvoicesAsync(actor, id, 50, cancellationToken);
                break;
        }
    }

    private static string NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "profile" => "profile",
        "certificates" => "certificates",
        "accounts" => "accounts",
        "projects" => "projects",
        "finance" => "finance",
        _ => "overview"
    };

    public sealed class AccountInput
    {
        public Guid? Id { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        public string? Number { get; set; }
        public string? BankName { get; set; }
        public string? Notes { get; set; }
        [Range(1, 3)]
        public int AccountType { get; set; } = (int)FinancialAccountType.Bank;
        public decimal OpeningBalance { get; set; }
        public bool DefaultCollection { get; set; }
        public bool DefaultPayment { get; set; }
        public bool DefaultInvoice { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class CertificateInput
    {
        public string Type { get; set; } = string.Empty;
        public string? Number { get; set; }
        public DateOnly? IssuedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        public string? Notes { get; set; }
    }
}
