using System.Security.Claims;
using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Backups;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class IndexModel(IBackupService backupService) : PageModel
{
    public IReadOnlyList<BackupTaskDto> Tasks { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) => Tasks = await backupService.ListAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await backupService.CreateBackupAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", cancellationToken);
        return RedirectToPage();
    }
}
