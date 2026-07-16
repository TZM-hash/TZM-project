using System.Security.Claims;
using EngineeringManager.Domain.Security;
using EngineeringManager.Application.EquipmentOffline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EngineeringManager.Domain.StageResults;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.EquipmentManager + "," + SystemRoles.SiteStaff)]
public sealed class OfflineModel(IEquipmentOfflineService service) : EquipmentPageModel
{
    public string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public void OnGet() { }
    public async Task<IActionResult> OnPostSyncAsync([FromBody] EquipmentOfflineSyncRequest request, CancellationToken token)
    {
        try { return new JsonResult(await service.SyncAsync(ResolveActor(), request, token)); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return BadRequest(new { error = exception.Message }); }
    }
    public async Task<IActionResult> OnPostPhotoAsync(Guid clientDraftId, Guid clientAttachmentId, IFormFile? photo, string? description, CancellationToken token)
    {
        if (photo is null || photo.Length == 0) return BadRequest(new { error = "请选择照片。" });
        try { await using var stream = photo.OpenReadStream(); return new JsonResult(await service.SyncPhotoAsync(ResolveActor(), new EquipmentOfflinePhotoRequest(clientDraftId, clientAttachmentId, photo.FileName, photo.ContentType, photo.Length, stream, AttachmentCategory.Photo, description), token)); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return BadRequest(new { error = exception.Message }); }
    }
}
