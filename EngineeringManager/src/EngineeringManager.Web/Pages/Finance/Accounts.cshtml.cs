using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Finance;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class AccountsModel(IFinanceLedgerService financeService, ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<FinancialAccountDto> Accounts { get; private set; } = [];
    public IReadOnlyList<FinanceOptionDto> LegalEntities { get; private set; } = [];
    public IReadOnlyList<EmployeeOption> Employees { get; private set; } = [];

    [BindProperty] public Guid LegalEntityId { get; set; }
    [BindProperty] public string AccountName { get; set; } = string.Empty;
    [BindProperty] public string? AccountNumber { get; set; }
    [BindProperty] public string? BankName { get; set; }
    [BindProperty] public FinancialAccountType AccountType { get; set; } = FinancialAccountType.Bank;
    [BindProperty] public decimal OpeningBalance { get; set; }
    [BindProperty] public string? OwnerName { get; set; }
    [BindProperty] public Guid? OwnerEmployeeId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await financeService.CreateAccountAsync(
                new CreateFinancialAccountRequest(LegalEntityId, AccountName, AccountNumber, BankName, AccountType, OpeningBalance, null, OwnerName, OwnerEmployeeId),
                cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Accounts = await financeService.ListAccountsAsync(cancellationToken);
        LegalEntities = (await financeService.GetEntryOptionsAsync(cancellationToken)).LegalEntities;
        Employees = await db.Employees.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.EmployeeNumber)
            .Select(item => new EmployeeOption(item.Id, item.EmployeeNumber + " · " + item.Name))
            .ToListAsync(cancellationToken);
    }

    public sealed record EmployeeOption(Guid Id, string Label);
}
