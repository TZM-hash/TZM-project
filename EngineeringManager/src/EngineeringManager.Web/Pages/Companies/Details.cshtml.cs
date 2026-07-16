using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(ICompanyManagementService companyService, ICompanyActorService actorService) : CompanyPageModel(actorService)
{
    public CompanyDetailsDto Company { get; private set; } = null!;
    public CompanyDashboardDto Dashboard { get; private set; } = null!;
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    [BindProperty] public AccountInput Account { get; set; } = new();
    [BindProperty] public CertificateInput Certificate { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Company = await companyService.GetAsync(actor, id, cancellationToken);
        Dashboard = await companyService.GetDashboardAsync(actor, id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        await companyService.SaveAccountAsync(actor, new SaveCompanyAccountRequest(null, id, Account.Name, Account.Number, Account.BankName,
            Account.AccountType, Account.OpeningBalance, Account.DefaultCollection, Account.DefaultPayment, Account.DefaultInvoice, true, null, "维护公司账户"), cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCertificateAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        await companyService.SaveCertificateAsync(actor, new SaveCompanyCertificateRequest(null, id, Certificate.Type, Certificate.Number,
            Certificate.IssuedOn, Certificate.ExpiresOn, null, Certificate.Notes, null, "维护公司证照"), cancellationToken);
        return RedirectToPage(new { id });
    }

    public sealed class AccountInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Number { get; set; }
        public string? BankName { get; set; }
        public int AccountType { get; set; } = (int)FinancialAccountType.Bank;
        public decimal OpeningBalance { get; set; }
        public bool DefaultCollection { get; set; }
        public bool DefaultPayment { get; set; }
        public bool DefaultInvoice { get; set; }
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
