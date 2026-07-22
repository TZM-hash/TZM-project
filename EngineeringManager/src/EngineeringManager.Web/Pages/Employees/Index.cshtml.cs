using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
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
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanManage => PageContext?.HttpContext?.User?.IsInRole(SystemRoles.SystemAdministrator) == true
        || PageContext?.HttpContext?.User?.IsInRole(SystemRoles.ApplicationAdministrator) == true;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public EmployeeType? EmployeeType { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        PageNumber = Math.Max(1, PageNumber);
        var all = await employeeService.ListAsync(Search, CanManage, cancellationToken);
        all = EmployeeType.HasValue
            ? all.Where(employee => employee.EmployeeType == EmployeeType.Value).ToArray()
            : all;
        TotalCount = all.Count;

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

    public string PageUrl(int page)
    {
        var pairs = Request.Query.SelectMany(item => item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)))
            .Where(item => !string.Equals(item.Key, nameof(PageNumber), StringComparison.OrdinalIgnoreCase))
            .Append(new KeyValuePair<string, string?>(nameof(PageNumber), page.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{Request.Path}{QueryString.Create(pairs)}";
    }
}
