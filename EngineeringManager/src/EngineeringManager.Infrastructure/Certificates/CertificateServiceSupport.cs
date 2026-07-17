using EngineeringManager.Application.Certificates;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Certificates;

internal static class CertificateServiceSupport
{
    private const int MaxAttachmentBytes = 20 * 1024 * 1024;

    public static string Required(string? value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("值不能为空。", parameterName)
        : value.Trim();

    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void ValidateDates(DateOnly? issuedOn, DateOnly? expiresOn, string parameterName)
    {
        if (issuedOn.HasValue && expiresOn.HasValue && expiresOn < issuedOn)
        {
            throw new ArgumentException("证书到期日期不能早于签发日期。", parameterName);
        }
    }

    public static async Task<Attachment> SaveAttachmentAsync(
        ApplicationDbContext db,
        IFileStore fileStore,
        CertificateAttachmentUpload upload,
        string userId,
        CancellationToken cancellationToken)
    {
        if (upload.Content.Length == 0 || upload.Content.Length > MaxAttachmentBytes)
        {
            throw new ArgumentException("证书附件不能为空且不能超过 20MB。", nameof(upload));
        }
        var originalName = Path.GetFileName(Required(upload.OriginalFileName, nameof(upload.OriginalFileName)));
        if (!string.Equals(originalName, upload.OriginalFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("附件文件名无效。", nameof(upload));
        }
        await using var stream = new MemoryStream(upload.Content, writable: false);
        var storedName = await fileStore.SaveAsync(stream, originalName, cancellationToken);
        var uploadedByUserId = await db.Users.AnyAsync(item => item.Id == userId, cancellationToken) ? userId : null;
        var attachment = new Attachment
        {
            StoredName = storedName,
            OriginalFileName = originalName,
            ContentType = Optional(upload.ContentType) ?? "application/octet-stream",
            SizeBytes = upload.Content.LongLength,
            Category = AttachmentCategory.General,
            Description = "证书附件",
            UploadedByUserId = uploadedByUserId
        };
        db.Attachments.Add(attachment);
        return attachment;
    }

    public static async Task<CertificateFileDto> DownloadAsync(Attachment? attachment, IFileStore fileStore, CancellationToken cancellationToken)
    {
        if (attachment is null || attachment.IsDeleted) throw new KeyNotFoundException("证书附件不存在。");
        await using var stream = await fileStore.OpenReadAsync(attachment.StoredName, cancellationToken);
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return new CertificateFileDto(attachment.OriginalFileName, attachment.ContentType, memory.ToArray());
    }

    public static async Task RemoveAttachmentAsync(Attachment? attachment, IFileStore fileStore, CancellationToken cancellationToken)
    {
        if (attachment is null || attachment.IsDeleted) return;
        attachment.IsDeleted = true;
        await fileStore.DeleteAsync(attachment.StoredName, cancellationToken);
    }
}
