using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(
    IEmployeeService employeeService,
    IBusinessYearService? businessYearService = null,
    IEmployeeAnnualLedgerService? annualLedgerService = null) : PageModel
{
    public IReadOnlyList<EmployeeDto> Employees { get; private set; } = [];
    public IReadOnlyDictionary<Guid, EmployeeAnnualLedgerSummary> AnnualSummaries { get; private set; } = new Dictionary<Guid, EmployeeAnnualLedgerSummary>();
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];
    public Guid? CurrentBusinessYearId { get; private set; }
    public decimal CurrentYearPayableTotal => AnnualSummaries.Values.Sum(item => item.CurrentYearNewPayable);
    public decimal CurrentYearPaidTotal => AnnualSummaries.Values.Sum(item => item.ReceivedAmount);
    public decimal CurrentYearUnpaidTotal => CurrentYearPayableTotal - CurrentYearPaidTotal;
    public int TotalCount { get; private set; }
    public int FormalCount { get; private set; }
    public int LaborCount { get; private set; }
    public int TemporaryCount { get; private set; }
    public int ActiveCount { get; private set; }
    public string? ActiveDialog { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanManage => PageContext?.HttpContext?.User?.IsInRole(SystemRoles.SystemAdministrator) == true
        || PageContext?.HttpContext?.User?.IsInRole(SystemRoles.ApplicationAdministrator) == true;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public EmployeeType? EmployeeType { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty] public EmployeeEditorInput Editor { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();

        if (!ModelState.IsValid)
        {
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            if (Editor.Id.HasValue)
            {
                await employeeService.UpdateAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                    new UpdateEmployeeRequest(
                        Editor.Id.Value, Editor.EmployeeNumber, Editor.Name, Editor.EmployeeType,
                        Editor.Phone, Editor.IdentityNumber, Editor.BankAccountNumber, Editor.BankName,
                        Editor.HireDate, Editor.LeaveDate, Editor.PositionTitle, Editor.DefaultLegalEntityId,
                        Editor.DefaultMonthlySalary, Editor.DefaultDailyRate, Editor.DefaultHourlyRate,
                        Editor.DefaultPieceworkRate, Editor.IsActive, Editor.ConcurrencyStamp,
                        Editor.Reason, Editor.Notes),
                    cancellationToken);
            }
            else
            {
                await employeeService.CreateAsync(
                    new CreateEmployeeRequest(
                        Editor.EmployeeNumber, Editor.Name, Editor.EmployeeType, Editor.Phone,
                        Editor.IdentityNumber, Editor.BankAccountNumber, Editor.BankName,
                        Editor.HireDate, Editor.LeaveDate, Editor.PositionTitle, Editor.DefaultLegalEntityId,
                        Editor.DefaultDailyRate, Editor.DefaultPieceworkRate, Editor.IsActive,
                        Editor.DefaultMonthlySalary, Editor.DefaultHourlyRate, Editor.Notes),
                    cancellationToken);
            }

            return RedirectToPage("/Employees/Index", new
            {
                Search,
                EmployeeType,
                PageNumber,
                PageSize
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        PageNumber = Math.Max(1, PageNumber);
        var all = await employeeService.ListAsync(Search, CanManage, cancellationToken);
        all = EmployeeType.HasValue
            ? all.Where(employee => employee.EmployeeType == EmployeeType.Value).ToArray()
            : all;
        TotalCount = all.Count;
        FormalCount = all.Count(employee => employee.EmployeeType == global::EngineeringManager.Domain.Employees.EmployeeType.Formal);
        LaborCount = all.Count(employee => employee.EmployeeType == global::EngineeringManager.Domain.Employees.EmployeeType.Labor);
        TemporaryCount = all.Count(employee => employee.EmployeeType == global::EngineeringManager.Domain.Employees.EmployeeType.Temporary);
        ActiveCount = all.Count(employee => employee.IsActive);

        if (businessYearService is not null && annualLedgerService is not null)
        {
            BusinessYears = await businessYearService.ListAsync(cancellationToken);
            var current = await businessYearService.GetByDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken)
                ?? (BusinessYears.Count > 0 ? BusinessYears[0] : null);
            if (current is not null)
            {
                CurrentBusinessYearId = current.Id;
                var summaries = new Dictionary<Guid, EmployeeAnnualLedgerSummary>();
                foreach (var employee in all)
                {
                    var ledger = await annualLedgerService.GetAnnualLedgerAsync(employee.Id, current.Id, cancellationToken);
                    summaries[employee.Id] = ledger.Summary;
                }

                AnnualSummaries = summaries;
            }
        }

        var skip = (PageNumber - 1) * PageSize;
        Employees = all.Skip(skip).Take(PageSize).ToArray();
        if (PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
            Employees = all.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToArray();
        }
    }

    public sealed class EmployeeEditorInput
    {
        public Guid? Id { get; set; }
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
        public string Reason { get; set; } = "维护员工资料";
        public string? Notes { get; set; }
    }

    public string PageUrl(int page)
    {
        var pairs = Request.Query.SelectMany(item => item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)))
            .Where(item => !string.Equals(item.Key, nameof(PageNumber), StringComparison.OrdinalIgnoreCase))
            .Append(new KeyValuePair<string, string?>(nameof(PageNumber), page.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{Request.Path}{QueryString.Create(pairs)}";
    }
}
