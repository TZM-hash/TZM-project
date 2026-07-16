using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class CreateModel(IEmployeeService employeeService) : PageModel
{
    [BindProperty] public string EmployeeNumber { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public EmployeeType EmployeeType { get; set; } = EmployeeType.Formal;
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? IdentityNumber { get; set; }
    [BindProperty] public string? BankAccountNumber { get; set; }
    [BindProperty] public string? BankName { get; set; }
    [BindProperty] public DateOnly? HireDate { get; set; }
    [BindProperty] public string? PositionTitle { get; set; }
    [BindProperty] public decimal? DefaultMonthlySalary { get; set; }
    [BindProperty] public decimal? DefaultDailyRate { get; set; }
    [BindProperty] public decimal? DefaultHourlyRate { get; set; }
    [BindProperty] public decimal? DefaultPieceworkRate { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.CreateAsync(
                new CreateEmployeeRequest(
                    EmployeeNumber,
                    Name,
                    EmployeeType,
                    Phone,
                    IdentityNumber,
                    BankAccountNumber,
                    BankName,
                    HireDate,
                    null,
                    PositionTitle,
                    null,
                    DefaultDailyRate,
                    DefaultPieceworkRate,
                    true,
                    DefaultMonthlySalary,
                    DefaultHourlyRate),
                cancellationToken);
            return RedirectToPage("/Employees/Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }
}
