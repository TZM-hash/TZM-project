using System.Security.Claims;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.DataExchange;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class TasksModel(IDataExchangeTaskService taskService) : PageModel
{
    public DataExchangeTaskPageDto TaskHistory { get; private set; } = new([], 1, 20, 0, 1);
    public IReadOnlyList<DataExchangeTaskItemDto> Tasks => TaskHistory.Items;
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);

    [BindProperty(SupportsGet = true)] public int HistoryPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int HistoryPageSize { get; set; } = 20;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TaskHistory = await taskService.ListAsync(
            new DataExchangeTaskQuery(UserId(), CanManage, HistoryPage, HistoryPageSize),
            cancellationToken);
    }

    public async Task<IActionResult> OnGetDownloadExportAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var file = await taskService.DownloadExportAsync(UserId(), CanManage, taskId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> OnGetDownloadImportErrorsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var file = await taskService.DownloadImportErrorsAsync(UserId(), CanManage, batchId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("当前用户没有标识。");
}
