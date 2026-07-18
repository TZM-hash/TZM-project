using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class LedgerModel(
    IEmployeeService employeeService,
    IBusinessYearService businessYearService,
    IEmployeeAnnualLedgerService annualLedgerService) : PageModel
{
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];
    public IReadOnlyList<EmployeeLedgerRow> Rows { get; private set; } = [];
    public BusinessYearDto? SelectedYear { get; private set; }

    [BindProperty(SupportsGet = true)]
    public Guid? BusinessYearId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        BusinessYears = await businessYearService.ListAsync(cancellationToken);
        SelectedYear = BusinessYearId.HasValue
            ? BusinessYears.SingleOrDefault(item => item.Id == BusinessYearId.Value)
            : await businessYearService.GetByDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken) ?? (BusinessYears.Count > 0 ? BusinessYears[0] : null);
        if (SelectedYear is null)
        {
            return;
        }

        BusinessYearId = SelectedYear.Id;
        var employees = await employeeService.ListAsync(null, cancellationToken);
        var rows = new List<EmployeeLedgerRow>(employees.Count);
        foreach (var employee in employees)
        {
            var ledger = await annualLedgerService.GetAnnualLedgerAsync(employee.Id, SelectedYear.Id, cancellationToken);
            rows.Add(new EmployeeLedgerRow(employee, ledger));
        }

        Rows = rows;
    }

    public sealed record EmployeeLedgerRow(EmployeeDto Employee, EmployeeAnnualLedgerDto Ledger);
}
