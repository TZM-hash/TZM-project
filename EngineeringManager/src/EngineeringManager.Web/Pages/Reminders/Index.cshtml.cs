using EngineeringManager.Application.Reminders;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Reminders;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IReminderService reminderService) : PageModel
{
    public IReadOnlyList<ReminderDto> Reminders { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await reminderService.RefreshAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        Reminders = await reminderService.ListAsync(false, cancellationToken);
    }

    public async Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken cancellationToken) { await reminderService.MarkReadAsync(id, cancellationToken); return RedirectToPage(); }
    public async Task<IActionResult> OnPostResolveAsync(Guid id, CancellationToken cancellationToken) { await reminderService.ResolveAsync(id, cancellationToken); return RedirectToPage(); }
    public static string SeverityLabel(ReminderSeverity severity) => severity switch
    {
        ReminderSeverity.Info => "轻度提醒",
        ReminderSeverity.Warning => "中度提醒",
        ReminderSeverity.Critical => "高度提醒",
        _ => severity.ToString()
    };
    public static string SeverityClass(ReminderSeverity severity) => severity.ToString().ToLowerInvariant();
}
