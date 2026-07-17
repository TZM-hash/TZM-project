using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees.Certificates;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IEmployeeCertificateService certificateService, IEmployeeService employeeService) : PageModel
{
    public IReadOnlyList<EmployeeCertificateDto> Certificates { get; private set; } = [];
    public IReadOnlyList<EmployeeDto> Employees { get; private set; } = [];
    public IReadOnlyList<string> CertificateTypes { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EmployeeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? CertificateType { get; set; }
    [BindProperty(SupportsGet = true)] public CertificateExpiryState? State { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Employees = await employeeService.ListAsync(null, cancellationToken);
        Certificates = await certificateService.ListAsync(new CertificateFilter(Search, EmployeeId, CertificateType, State), Today(), cancellationToken);
        CertificateTypes = (await certificateService.ListAsync(new CertificateFilter(), Today(), cancellationToken)).Select(item => item.CertificateType).Distinct().Order().ToArray();
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await certificateService.DownloadAttachmentAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid concurrencyStamp, CancellationToken cancellationToken)
    {
        await certificateService.DeleteAsync(User.Identity?.Name ?? "unknown", CanManage, id, concurrencyStamp, "管理员删除员工证书", cancellationToken);
        return RedirectToPage();
    }

    public static string StatusLabel(CertificateExpiryState state) => state switch
    {
        CertificateExpiryState.LongTerm => "长期有效",
        CertificateExpiryState.Normal => "有效",
        CertificateExpiryState.Info => "轻度提醒",
        CertificateExpiryState.Warning => "中度提醒",
        CertificateExpiryState.Critical => "重度提醒",
        CertificateExpiryState.Expired => "已过期",
        _ => state.ToString()
    };
    public static string StatusClass(CertificateExpiryState state) => state.ToString().ToLowerInvariant();
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
}
