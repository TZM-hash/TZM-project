using System.Security.Claims;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IEmployeeService employeeService) : PageModel
{
    public IReadOnlyList<EmployeeDto> Employees { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public bool QuickEditOpen { get; private set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public EmployeeType? EmployeeType { get; set; }
    [BindProperty] public QuickEditInput QuickEdit { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadEmployeesAsync(cancellationToken);

    public async Task<IActionResult> OnPostQuickEditAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var existing = await employeeService.GetAsync(QuickEdit.Id, cancellationToken);
            await employeeService.UpdateAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new UpdateEmployeeRequest(
                    QuickEdit.Id,
                    QuickEdit.EmployeeNumber,
                    QuickEdit.Name,
                    QuickEdit.EmployeeType,
                    QuickEdit.Phone,
                    QuickEdit.IdentityNumber,
                    QuickEdit.BankAccountNumber,
                    QuickEdit.BankName,
                    QuickEdit.HireDate,
                    QuickEdit.LeaveDate,
                    QuickEdit.PositionTitle,
                    QuickEdit.DefaultLegalEntityId,
                    QuickEdit.DefaultMonthlySalary,
                    QuickEdit.DefaultDailyRate,
                    QuickEdit.DefaultHourlyRate,
                    QuickEdit.DefaultPieceworkRate,
                    QuickEdit.IsActive,
                    QuickEdit.ConcurrencyStamp,
                    QuickEdit.Reason,
                    existing?.Notes),
                cancellationToken);
            return RedirectToPage(new { search = Search, employeeType = EmployeeType });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            QuickEditOpen = true;
            await LoadEmployeesAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadEmployeesAsync(CancellationToken cancellationToken)
    {
        var employees = await employeeService.ListAsync(Search, cancellationToken);
        Employees = EmployeeType.HasValue
            ? employees.Where(employee => employee.EmployeeType == EmployeeType.Value).ToList()
            : employees;
    }

    public sealed class QuickEditInput
    {
        public Guid Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public EmployeeType EmployeeType { get; set; } = EmployeeType.Formal;
        public string? Phone { get; set; }
        public string? IdentityNumber { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankName { get; set; }
        public DateOnly? HireDate { get; set; }
        public DateOnly? LeaveDate { get; set; }
        public string? PositionTitle { get; set; }
        public Guid? DefaultLegalEntityId { get; set; }
        public decimal? DefaultMonthlySalary { get; set; }
        public decimal? DefaultDailyRate { get; set; }
        public decimal? DefaultHourlyRate { get; set; }
        public decimal? DefaultPieceworkRate { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "快捷编辑员工资料";
        public string? Notes { get; set; }
    }
}
