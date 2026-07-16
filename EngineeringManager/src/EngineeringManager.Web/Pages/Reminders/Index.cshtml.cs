using EngineeringManager.Application.Reminders;
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
}
