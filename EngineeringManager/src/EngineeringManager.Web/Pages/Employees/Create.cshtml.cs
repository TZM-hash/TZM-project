using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class CreateModel(IEmployeeService employeeService) : PageModel
{
    public bool IsEditing => Id.HasValue;
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public string EmployeeNumber { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public EmployeeType EmployeeType { get; set; } = EmployeeType.Formal;
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? IdentityNumber { get; set; }
    [BindProperty] public string? BankAccountNumber { get; set; }
    [BindProperty] public string? BankName { get; set; }
    [BindProperty] public DateOnly? HireDate { get; set; }
    [BindProperty] public DateOnly? LeaveDate { get; set; }
    [BindProperty] public string? PositionTitle { get; set; }
    [BindProperty] public decimal? DefaultMonthlySalary { get; set; }
    [BindProperty] public decimal? DefaultDailyRate { get; set; }
    [BindProperty] public decimal? DefaultHourlyRate { get; set; }
    [BindProperty] public decimal? DefaultPieceworkRate { get; set; }
    [BindProperty] public Guid? DefaultLegalEntityId { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public Guid ConcurrencyStamp { get; set; }
    [BindProperty] public string Reason { get; set; } = "维护员工资料";

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? copyFrom, CancellationToken token)
    {
        var sourceId = id ?? copyFrom;
        if (!sourceId.HasValue) return Page();
        var employee = await employeeService.GetAsync(sourceId.Value, token);
        if (employee is null) return NotFound();
        Populate(employee);
        if (copyFrom.HasValue)
        {
            Id = null; EmployeeNumber += "-COPY"; Name += "（复制）"; Phone = null; IdentityNumber = null; BankAccountNumber = null; BankName = null; HireDate = null; LeaveDate = null; ConcurrencyStamp = Guid.Empty; Reason = "复制员工档案";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Id.HasValue)
                await employeeService.UpdateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", new UpdateEmployeeRequest(Id.Value, EmployeeNumber, Name, EmployeeType, Phone, IdentityNumber, BankAccountNumber, BankName, HireDate, LeaveDate, PositionTitle, DefaultLegalEntityId, DefaultMonthlySalary, DefaultDailyRate, DefaultHourlyRate, DefaultPieceworkRate, IsActive, ConcurrencyStamp, Reason), cancellationToken);
            else
                await employeeService.CreateAsync(new CreateEmployeeRequest(EmployeeNumber, Name, EmployeeType, Phone, IdentityNumber, BankAccountNumber, BankName, HireDate, LeaveDate, PositionTitle, DefaultLegalEntityId, DefaultDailyRate, DefaultPieceworkRate, IsActive, DefaultMonthlySalary, DefaultHourlyRate), cancellationToken);
            return RedirectToPage("/Employees/Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private void Populate(EmployeeDto item)
    {
        Id = item.Id; EmployeeNumber = item.EmployeeNumber; Name = item.Name; EmployeeType = item.EmployeeType; Phone = item.Phone; IdentityNumber = item.IdentityNumber; BankAccountNumber = item.BankAccountNumber; BankName = item.BankName; HireDate = item.HireDate; LeaveDate = item.LeaveDate; PositionTitle = item.PositionTitle; DefaultLegalEntityId = item.DefaultLegalEntityId; DefaultMonthlySalary = item.DefaultMonthlySalary; DefaultDailyRate = item.DefaultDailyRate; DefaultHourlyRate = item.DefaultHourlyRate; DefaultPieceworkRate = item.DefaultPieceworkRate; IsActive = item.IsActive; ConcurrencyStamp = item.ConcurrencyStamp;
    }
}
