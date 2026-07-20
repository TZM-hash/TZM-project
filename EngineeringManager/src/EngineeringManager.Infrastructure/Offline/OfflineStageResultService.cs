using EngineeringManager.Application.Offline;
using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Offline;

public sealed class OfflineStageResultService(ApplicationDbContext db, IFileStore fileStore) : IOfflineStageResultService
{
    public async Task<IReadOnlyList<OfflineProjectOptionDto>> GetProjectOptionsAsync(OfflineSyncActor actor, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(actor, cancellationToken);
        var projects = db.Projects.AsNoTracking()
            .Include(item => item.Contracts.Where(contract => contract.IsActive))
                .ThenInclude(contract => contract.LineItems)
            .Where(item => item.IsActive);
        if (!actor.CanAccessAllProjects)
        {
            projects = projects.Where(item => item.ResponsibleUserId == actor.UserId || item.Assignments.Any(assignment => assignment.UserId == actor.UserId));
        }

        var items = await projects.OrderBy(item => item.ProjectNumber).ToListAsync(cancellationToken);
        return items.Select(project => new OfflineProjectOptionDto(
            project.Id,
            project.ProjectNumber,
            project.Name,
            project.Contracts.OrderBy(contract => contract.ContractNumber).Select(contract => new OfflineContractOptionDto(
                contract.Id,
                contract.ContractNumber,
                contract.Name,
                contract.LineItems.OrderBy(line => line.Code).Select(line => new OfflineLineItemOptionDto(line.Id, line.Code, line.Name, line.Unit)).ToArray())).ToArray())).ToArray();
    }

    public async Task<OfflineDraftSyncResultDto> SyncDraftAsync(OfflineSyncActor actor, OfflineDraftSyncRequest request, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(actor, cancellationToken);
        await ValidateProjectAccessAsync(actor, request.ProjectId, cancellationToken);
        if (request.ClientDraftId == Guid.Empty || request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("客户端草稿 ID 和操作 ID 不能为空。", nameof(request));
        }

        var sync = await db.OfflineDraftSyncs
            .Include(item => item.StageResult).ThenInclude(result => result.Lines)
            .SingleOrDefaultAsync(item => item.UserId == actor.UserId && item.ClientDraftId == request.ClientDraftId, cancellationToken);
        if (sync is not null && sync.LastOperationId == request.OperationId)
        {
            return Result(sync, isIdempotent: true, sync.Status == OfflineSyncStatus.Conflict ? Snapshot(sync.StageResult) : null);
        }

        if (sync is null && request.ServerStageResultId.HasValue)
        {
            throw new InvalidOperationException("本机草稿尚未建立服务器映射，不能指定服务器记录。");
        }

        if (sync is not null)
        {
            if (request.ServerStageResultId != sync.StageResultId)
            {
                throw new InvalidOperationException("服务器阶段成果与客户端草稿映射不一致。");
            }

            if (sync.StageResult.Status != StageResultStatus.Draft)
            {
                return await FailAsync(sync, "服务器阶段成果已不是草稿，不能由离线内容覆盖。", cancellationToken);
            }

            if (!request.BaseServerVersion.HasValue || request.BaseServerVersion.Value != sync.StageResult.ConcurrencyStamp)
            {
                sync.Status = OfflineSyncStatus.Conflict;
                sync.LastError = "服务器草稿已被其他操作修改，请比较后再处理。";
                sync.LastAttemptAt = DateTimeOffset.UtcNow;
                sync.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return Result(sync, isIdempotent: false, Snapshot(sync.StageResult));
            }
        }

        var preparedLines = await PrepareLinesAsync(request, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (sync is null)
        {
            var result = new StageResult
            {
                ProjectId = request.ProjectId,
                ContractId = request.ContractId,
                Title = NormalizeRequired(request.Title, nameof(request.Title)),
                ResultType = request.ResultType,
                Status = StageResultStatus.Draft,
                ResultDate = request.ResultDate,
                Description = NormalizeOptional(request.Description),
                QualityResult = request.QualityResult,
                SubmittedByUserId = actor.UserId,
                IsOfflineDraft = true
            };
            AddLines(result, preparedLines);
            sync = new OfflineDraftSync
            {
                UserId = actor.UserId,
                ClientDraftId = request.ClientDraftId,
                LastOperationId = request.OperationId,
                StageResult = result,
                LastServerVersion = result.ConcurrencyStamp,
                Status = OfflineSyncStatus.Synced,
                LastAttemptAt = DateTimeOffset.UtcNow,
                LastSyncedAt = DateTimeOffset.UtcNow
            };
            db.OfflineDraftSyncs.Add(sync);
        }
        else
        {
            var result = sync.StageResult;
            result.ProjectId = request.ProjectId;
            result.ContractId = request.ContractId;
            result.Title = NormalizeRequired(request.Title, nameof(request.Title));
            result.ResultType = request.ResultType;
            result.ResultDate = request.ResultDate;
            result.Description = NormalizeOptional(request.Description);
            result.QualityResult = request.QualityResult;
            result.UpdatedAt = DateTimeOffset.UtcNow;
            result.ConcurrencyStamp = Guid.NewGuid();
            db.StageResultLines.RemoveRange(result.Lines);
            result.Lines.Clear();
            var newLines = AddLines(result, preparedLines);
            db.StageResultLines.AddRange(newLines);
            sync.LastOperationId = request.OperationId;
            sync.LastServerVersion = result.ConcurrencyStamp;
            sync.Status = OfflineSyncStatus.Synced;
            sync.LastError = null;
            sync.LastAttemptAt = DateTimeOffset.UtcNow;
            sync.LastSyncedAt = DateTimeOffset.UtcNow;
            sync.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        sync.LastServerVersion = sync.StageResult.ConcurrencyStamp;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result(sync, isIdempotent: false, null);
    }

    public async Task<OfflinePhotoSyncResultDto> SyncPhotoAsync(OfflineSyncActor actor, OfflinePhotoSyncRequest request, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(actor, cancellationToken);
        var sync = await db.OfflineDraftSyncs
            .Include(item => item.StageResult)
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.UserId == actor.UserId && item.ClientDraftId == request.ClientDraftId, cancellationToken)
            ?? throw new InvalidOperationException("请先同步阶段成果草稿，再上传照片。");
        await ValidateProjectAccessAsync(actor, sync.StageResult.ProjectId, cancellationToken);
        var existing = sync.Attachments.SingleOrDefault(item => item.ClientAttachmentId == request.ClientAttachmentId);
        if (existing is not null)
        {
            return new OfflinePhotoSyncResultDto(existing.AttachmentId, true);
        }

        OfflinePhotoPolicy.Validate(sync.Attachments.Count + 1, request.SizeBytes);
        if (request.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            throw new ArgumentException("离线附件只支持 JPEG、PNG 或 WebP 照片。", nameof(request));
        }

        var safeName = Path.GetFileName(NormalizeRequired(request.OriginalFileName, nameof(request.OriginalFileName)));
        var storedName = await fileStore.SaveAsync(request.Content, safeName, cancellationToken);
        try
        {
            var attachment = new Attachment
            {
                ProjectId = sync.StageResult.ProjectId,
                ContractId = sync.StageResult.ContractId,
                StageResultId = sync.StageResultId,
                StoredName = storedName,
                OriginalFileName = safeName,
                ContentType = request.ContentType,
                SizeBytes = request.SizeBytes,
                Category = request.Category,
                Description = NormalizeOptional(request.Description),
                UploadedByUserId = actor.UserId
            };
            var mapping = new OfflineAttachmentSync
            {
                DraftSync = sync,
                ClientAttachmentId = request.ClientAttachmentId,
                Attachment = attachment
            };
            db.OfflineAttachmentSyncs.Add(mapping);
            sync.LastAttemptAt = DateTimeOffset.UtcNow;
            sync.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new OfflinePhotoSyncResultDto(attachment.Id, false);
        }
        catch
        {
            await fileStore.DeleteAsync(storedName, CancellationToken.None);
            throw;
        }
    }

    public async Task ReportFailureAsync(OfflineSyncActor actor, Guid clientDraftId, string errorMessage, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(actor, cancellationToken);
        var sync = await db.OfflineDraftSyncs.SingleOrDefaultAsync(item => item.UserId == actor.UserId && item.ClientDraftId == clientDraftId, cancellationToken)
            ?? throw new InvalidOperationException("离线草稿同步记录不存在。");
        sync.Status = OfflineSyncStatus.Failed;
        sync.LastError = NormalizeRequired(errorMessage, nameof(errorMessage));
        sync.LastAttemptAt = DateTimeOffset.UtcNow;
        sync.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<PreparedLine>> PrepareLinesAsync(OfflineDraftSyncRequest request, CancellationToken cancellationToken)
    {
        var lineRequests = request.Lines.ToArray();
        if (lineRequests.Select(item => item.ContractLineItemId).Distinct().Count() != lineRequests.Length)
        {
            throw new ArgumentException("同一离线草稿不能重复填写同一清单项。", nameof(request));
        }

        if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(item => item.Id == request.ContractId && item.ProjectId == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("合同不存在或不属于所选项目。");
        }

        if (lineRequests.Length == 0) return [];
        var ids = lineRequests.Select(item => item.ContractLineItemId).ToArray();
        var items = await db.ContractLineItems.Include(item => item.Contract).Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (items.Count != ids.Length || items.Values.Any(item => item.Contract.ProjectId != request.ProjectId || request.ContractId.HasValue && item.ContractId != request.ContractId))
        {
            throw new InvalidOperationException("工程量清单项不存在或不属于所选项目/合同。");
        }

        var previous = await db.StageResultLines.Where(item => ids.Contains(item.ContractLineItemId) && item.StageResult.Status == StageResultStatus.Recorded)
            .GroupBy(item => item.ContractLineItemId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(item => item.PeriodQuantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);
        return lineRequests.Select(line =>
        {
            var item = items[line.ContractLineItemId];
            var target = item.Quantity ?? 0m;
            var quantity = StageQuantityCalculator.Calculate(target, previous.GetValueOrDefault(line.ContractLineItemId), line.PeriodQuantity);
            return new PreparedLine(line, quantity);
        }).ToList();
    }

    private static List<StageResultLine> AddLines(StageResult result, IEnumerable<PreparedLine> lines)
    {
        var added = new List<StageResultLine>();
        foreach (var prepared in lines)
        {
            var line = new StageResultLine
            {
                StageResult = result,
                ContractLineItemId = prepared.Request.ContractLineItemId,
                PeriodQuantity = prepared.Request.PeriodQuantity,
                CumulativeQuantity = prepared.Quantity.CumulativeQuantity,
                RemainingQuantity = prepared.Quantity.RemainingQuantity,
                CompletionPercentage = prepared.Quantity.CompletionPercentage,
                ExceedsTarget = prepared.Quantity.ExceedsTarget,
                Notes = NormalizeOptional(prepared.Request.Notes)
            };
            result.Lines.Add(line);
            added.Add(line);
        }

        return added;
    }

    private async Task ValidateActorAsync(OfflineSyncActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actor.UserId) || !await db.Users.AnyAsync(item => item.Id == actor.UserId && item.IsEnabled, cancellationToken))
        {
            throw new UnauthorizedAccessException("当前用户不存在或已停用。");
        }
    }

    private async Task ValidateProjectAccessAsync(OfflineSyncActor actor, Guid projectId, CancellationToken cancellationToken)
    {
        var allowed = await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive &&
            (actor.CanAccessAllProjects || item.ResponsibleUserId == actor.UserId || item.Assignments.Any(assignment => assignment.UserId == actor.UserId)), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("当前用户没有该项目的离线录入权限。");
    }

    private async Task<OfflineDraftSyncResultDto> FailAsync(OfflineDraftSync sync, string message, CancellationToken cancellationToken)
    {
        sync.Status = OfflineSyncStatus.Failed;
        sync.LastError = message;
        sync.LastAttemptAt = DateTimeOffset.UtcNow;
        sync.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Result(sync, false, Snapshot(sync.StageResult));
    }

    private static OfflineDraftSyncResultDto Result(OfflineDraftSync sync, bool isIdempotent, OfflineDraftSnapshotDto? snapshot) =>
        new(sync.Status, sync.StageResultId, sync.StageResult.ConcurrencyStamp, isIdempotent, snapshot, sync.LastError);

    private static OfflineDraftSnapshotDto Snapshot(StageResult result) =>
        new(result.Id, result.ConcurrencyStamp, result.ProjectId, result.ContractId, result.Title, result.ResultType, result.ResultDate,
            result.Description, result.QualityResult,
            result.Lines.Select(line => new OfflineLineSnapshotDto(line.ContractLineItemId, line.PeriodQuantity, line.Notes)).ToArray());

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("值不能为空。", parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record PreparedLine(OfflineLineItemRequest Request, StageQuantitySummary Quantity);
}
