using System.Data;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Projects;

public sealed class ProjectRecordAttachmentService(ApplicationDbContext db, IFileStore fileStore) : IProjectRecordAttachmentService
{
    public async Task<IReadOnlyList<ProjectRecordAttachmentDto>> ListAsync(Guid projectId, ProjectRecordAttachmentType recordType, Guid recordId, CancellationToken token) =>
        (await Query(projectId, recordType, recordId).AsNoTracking().ToListAsync(token)).OrderByDescending(item => item.UploadedAt).Select(ToDto).ToArray();

    public async Task<ProjectRecordAttachmentDto> UploadAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token)
    {
        var safeName = ValidateUpload(actor, upload);
        await EnsureRecordAsync(upload.ProjectId, upload.RecordType, upload.RecordId, token);
        await using var content = new MemoryStream(upload.Content, writable: false);
        var storedName = await fileStore.SaveAsync(content, safeName, token);
        try
        {
            var item = await CreateAttachmentAsync(actor, upload, safeName, storedName, token);
            db.Attachments.Add(item);
            await db.SaveChangesAsync(token);
            return ToDto(item);
        }
        catch { await fileStore.DeleteAsync(storedName, token); throw; }
    }

    public async Task<ProjectRecordAttachmentDto> ReplaceAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token)
    {
        var safeName = ValidateUpload(actor, upload);
        await EnsureRecordAsync(upload.ProjectId, upload.RecordType, upload.RecordId, token);
        await using var content = new MemoryStream(upload.Content, writable: false);
        var storedName = await fileStore.SaveAsync(content, safeName, token);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var existing = await Query(upload.ProjectId, upload.RecordType, upload.RecordId).ToListAsync(token);
            foreach (var item in existing) item.IsDeleted = true;
            var replacement = await CreateAttachmentAsync(actor, upload, safeName, storedName, token);
            db.Attachments.Add(replacement);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return ToDto(replacement);
        }
        catch
        {
            await fileStore.DeleteAsync(storedName, token);
            throw;
        }
    }

    public Task<ProjectRecordAttachmentDto> ReplaceQuantityAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token) =>
        ReplaceAsync(actor, upload, token);

    public async Task<ProjectRecordAttachmentFile> DownloadAsync(Guid projectId, Guid attachmentId, CancellationToken token)
    {
        var item = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == attachmentId && value.ProjectId == projectId && !value.IsDeleted, token) ?? throw new KeyNotFoundException("附件不存在。");
        return new ProjectRecordAttachmentFile(item.OriginalFileName, item.ContentType, await fileStore.OpenReadAsync(item.StoredName, token));
    }

    public async Task DeleteAsync(ProjectRecordAttachmentActor actor, Guid projectId, Guid attachmentId, CancellationToken token)
    {
        if (!actor.CanManage) throw new UnauthorizedAccessException("没有附件管理权限。");
        var item = await db.Attachments.SingleOrDefaultAsync(value => value.Id == attachmentId && value.ProjectId == projectId && !value.IsDeleted, token) ?? throw new KeyNotFoundException("附件不存在。");
        item.IsDeleted = true;
        await db.SaveChangesAsync(token);
    }

    private IQueryable<Attachment> Query(Guid projectId, ProjectRecordAttachmentType type, Guid id) => type switch
    {
        ProjectRecordAttachmentType.Quantity => db.Attachments.Where(item => item.ProjectId == projectId && item.ContractLineItemId == id && !item.IsDeleted),
        ProjectRecordAttachmentType.Settlement => db.Attachments.Where(item => item.ProjectId == projectId && item.FinanceSettlementId == id && !item.IsDeleted),
        ProjectRecordAttachmentType.Invoice => db.Attachments.Where(item => item.ProjectId == projectId && item.FinanceInvoiceId == id && !item.IsDeleted),
        ProjectRecordAttachmentType.Cash => db.Attachments.Where(item => item.ProjectId == projectId && item.FinanceCashEntryId == id && !item.IsDeleted),
        ProjectRecordAttachmentType.Construction => db.Attachments.Where(item => item.ProjectId == projectId && item.ProjectConstructionRecordId == id && !item.IsDeleted),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private async Task EnsureRecordAsync(Guid projectId, ProjectRecordAttachmentType type, Guid id, CancellationToken token)
    {
        var exists = type switch
        {
            ProjectRecordAttachmentType.Quantity => await db.ContractLineItems.AnyAsync(item => item.Id == id && item.Contract.ProjectId == projectId, token),
            ProjectRecordAttachmentType.Settlement => await db.FinanceSettlements.AnyAsync(item => item.Id == id && item.ProjectId == projectId, token),
            ProjectRecordAttachmentType.Invoice => await db.FinanceInvoices.AnyAsync(item => item.Id == id && item.Allocations.Any(allocation => allocation.ProjectId == projectId), token),
            ProjectRecordAttachmentType.Cash => await db.FinanceCashEntries.AnyAsync(item => item.Id == id &&
                (item.Allocations.Any(allocation => allocation.ProjectId == projectId) ||
                 item.SourceType == LedgerSourceType.ProjectCollection && item.SourceId == projectId), token),
            ProjectRecordAttachmentType.Construction => await db.ProjectConstructionRecords.AnyAsync(item => item.Id == id && item.ProjectId == projectId, token),
            _ => false
        };
        if (!exists) throw new KeyNotFoundException("项目业务明细不存在。");
    }

    private static string ValidateUpload(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload)
    {
        if (!actor.CanManage) throw new UnauthorizedAccessException("没有附件管理权限。");
        if (upload.Content.Length is 0 or > 20 * 1024 * 1024) throw new ArgumentException("附件不能为空且不能超过 20MB。", nameof(upload));
        var safeName = Path.GetFileName(upload.OriginalFileName);
        if (string.IsNullOrWhiteSpace(safeName) || !string.Equals(safeName, upload.OriginalFileName, StringComparison.Ordinal)) throw new ArgumentException("附件文件名无效。", nameof(upload));
        return safeName;
    }

    private async Task<Attachment> CreateAttachmentAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, string safeName, string storedName, CancellationToken token)
    {
        var uploadedByUserId = await db.Users.AnyAsync(item => item.Id == actor.UserId, token) ? actor.UserId : null;
        var item = new Attachment
        {
            ProjectId = upload.ProjectId,
            StoredName = storedName,
            OriginalFileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType.Trim(),
            SizeBytes = upload.Content.LongLength,
            Category = AttachmentCategory.General,
            Description = upload.Description?.Trim(),
            UploadedByUserId = uploadedByUserId
        };
        SetTarget(item, upload.RecordType, upload.RecordId);
        return item;
    }

    private static void SetTarget(Attachment item, ProjectRecordAttachmentType type, Guid id)
    {
        if (type == ProjectRecordAttachmentType.Quantity) item.ContractLineItemId = id;
        else if (type == ProjectRecordAttachmentType.Settlement) item.FinanceSettlementId = id;
        else if (type == ProjectRecordAttachmentType.Invoice) item.FinanceInvoiceId = id;
        else if (type == ProjectRecordAttachmentType.Cash) item.FinanceCashEntryId = id;
        else if (type == ProjectRecordAttachmentType.Construction) item.ProjectConstructionRecordId = id;
        else throw new ArgumentOutOfRangeException(nameof(type));
    }

    private static ProjectRecordAttachmentDto ToDto(Attachment item)
    {
        var (type, id) = item.ContractLineItemId.HasValue ? (ProjectRecordAttachmentType.Quantity, item.ContractLineItemId.Value)
            : item.FinanceSettlementId.HasValue ? (ProjectRecordAttachmentType.Settlement, item.FinanceSettlementId.Value)
            : item.FinanceInvoiceId.HasValue ? (ProjectRecordAttachmentType.Invoice, item.FinanceInvoiceId.Value)
            : item.FinanceCashEntryId.HasValue ? (ProjectRecordAttachmentType.Cash, item.FinanceCashEntryId.Value)
            : item.ProjectConstructionRecordId.HasValue ? (ProjectRecordAttachmentType.Construction, item.ProjectConstructionRecordId.Value)
            : throw new InvalidOperationException("附件没有业务明细关联。");
        return new ProjectRecordAttachmentDto(item.Id, item.ProjectId ?? Guid.Empty, type, id, item.OriginalFileName, item.ContentType, item.SizeBytes, item.Description, item.UploadedAt);
    }
}
