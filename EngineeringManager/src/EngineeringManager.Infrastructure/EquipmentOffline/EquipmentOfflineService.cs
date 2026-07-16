using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.EquipmentOffline;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Domain.Offline;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.EquipmentOffline;

public sealed class EquipmentOfflineService(ApplicationDbContext db, IEquipmentService equipmentService, IFileStore? fileStore = null) : IEquipmentOfflineService
{
    public async Task<EquipmentOfflineSyncResult> SyncAsync(EquipmentActor actor, EquipmentOfflineSyncRequest request, CancellationToken token)
    {
        if (request.ClientDraftId == Guid.Empty || request.OperationId == Guid.Empty) throw new ArgumentException("客户端草稿 ID 和操作 ID 不能为空。");
        if (request.Usage.UnitRate != 0m) throw new InvalidOperationException("租金、单价和结算数据不能通过离线端点提交。");
        var mapping = await db.OfflineEquipmentUsageSyncs.Include(item => item.EquipmentProjectUsage).SingleOrDefaultAsync(item => item.UserId == actor.UserId && item.ClientDraftId == request.ClientDraftId, token);
        if (mapping is not null && mapping.LastOperationId == request.OperationId)
            return new EquipmentOfflineSyncResult(mapping.EquipmentProjectUsageId, mapping.LastServerVersion, true, false, null);
        if (mapping is not null && (!request.BaseServerVersion.HasValue || request.BaseServerVersion != mapping.EquipmentProjectUsage.ConcurrencyStamp))
        {
            mapping.Status = "Conflict";
            mapping.LastError = "服务器设备使用记录已变化，请联网比较后处理。";
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(token);
            return new EquipmentOfflineSyncResult(mapping.EquipmentProjectUsageId, mapping.EquipmentProjectUsage.ConcurrencyStamp, false, true, mapping.LastError);
        }
        var usageRequest = request.Usage with { Id = mapping?.EquipmentProjectUsageId, ConcurrencyStamp = mapping?.EquipmentProjectUsage.ConcurrencyStamp, UnitRate = 0m };
        var saved = await equipmentService.SaveUsageAsync(actor, usageRequest, token);
        if (mapping is null)
        {
            mapping = new OfflineEquipmentUsageSync { UserId = actor.UserId, ClientDraftId = request.ClientDraftId, EquipmentProjectUsageId = saved.Id };
            db.OfflineEquipmentUsageSyncs.Add(mapping);
        }
        mapping.LastOperationId = request.OperationId;
        mapping.LastServerVersion = saved.ConcurrencyStamp;
        mapping.Status = "Synced";
        mapping.LastError = null;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        return new EquipmentOfflineSyncResult(saved.Id, saved.ConcurrencyStamp, false, false, null);
    }

    public async Task<EquipmentOfflinePhotoResult> SyncPhotoAsync(EquipmentActor actor, EquipmentOfflinePhotoRequest request, CancellationToken token)
    {
        var mapping = await db.OfflineEquipmentUsageSyncs.Include(item => item.EquipmentProjectUsage).Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.UserId == actor.UserId && item.ClientDraftId == request.ClientDraftId, token)
            ?? throw new InvalidOperationException("请先同步设备现场草稿，再上传照片。");
        var existing = mapping.Attachments.SingleOrDefault(item => item.ClientAttachmentId == request.ClientAttachmentId);
        if (existing is not null) return new EquipmentOfflinePhotoResult(existing.AttachmentId, true);
        OfflinePhotoPolicy.Validate(mapping.Attachments.Count + 1, request.SizeBytes);
        if (request.ContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("设备离线附件只支持 JPEG、PNG 或 WebP 照片。");
        var store = fileStore ?? throw new InvalidOperationException("附件存储未配置。");
        var safeName = Path.GetFileName(request.OriginalFileName);
        var storedName = await store.SaveAsync(request.Content, safeName, token);
        try
        {
            var attachment = new Attachment { ProjectId = mapping.EquipmentProjectUsage.ProjectId, StoredName = storedName, OriginalFileName = safeName, ContentType = request.ContentType, SizeBytes = request.SizeBytes, Category = request.Category, Description = request.Description, UploadedByUserId = actor.UserId };
            var photo = new OfflineEquipmentAttachmentSync { UsageSync = mapping, ClientAttachmentId = request.ClientAttachmentId, Attachment = attachment };
            db.OfflineEquipmentAttachmentSyncs.Add(photo);
            await db.SaveChangesAsync(token);
            return new EquipmentOfflinePhotoResult(attachment.Id, false);
        }
        catch { await store.DeleteAsync(storedName, CancellationToken.None); throw; }
    }
}
