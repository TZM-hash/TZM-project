using System.Security.Claims;
using EngineeringManager.Application.Offline;
using EngineeringManager.Domain.Security;
using EngineeringManager.Domain.StageResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.StageResults;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff)]
public sealed class OfflineModel(IOfflineStageResultService offlineService) : PageModel
{
    public IReadOnlyList<OfflineProjectOptionDto> Projects { get; private set; } = [];
    public string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Projects = await offlineService.GetProjectOptionsAsync(Actor(), cancellationToken);
    }

    public async Task<IActionResult> OnGetOptionsAsync(CancellationToken cancellationToken) =>
        new JsonResult(await offlineService.GetProjectOptionsAsync(Actor(), cancellationToken));

    public async Task<IActionResult> OnPostSyncAsync([FromBody] OfflineDraftSyncRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await offlineService.SyncDraftAsync(Actor(), request, cancellationToken));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IActionResult> OnPostPhotoAsync(
        Guid clientDraftId,
        Guid clientAttachmentId,
        IFormFile? photo,
        AttachmentCategory category = AttachmentCategory.Photo,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (photo is null || photo.Length == 0) return BadRequest(new { error = "请选择照片。" });
        try
        {
            await using var content = photo.OpenReadStream();
            var result = await offlineService.SyncPhotoAsync(
                Actor(),
                new OfflinePhotoSyncRequest(clientDraftId, clientAttachmentId, photo.FileName, photo.ContentType, photo.Length, content, category, description),
                cancellationToken);
            return new JsonResult(result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IActionResult> OnPostFailureAsync([FromBody] OfflineFailureReport report, CancellationToken cancellationToken)
    {
        try
        {
            await offlineService.ReportFailureAsync(Actor(), report.ClientDraftId, report.ErrorMessage, cancellationToken);
            return new OkResult();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private OfflineSyncActor Actor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("当前用户没有标识。");
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
        return new OfflineSyncActor(userId, canAccessAll);
    }
}
