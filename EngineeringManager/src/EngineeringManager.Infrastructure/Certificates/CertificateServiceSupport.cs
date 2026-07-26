using EngineeringManager.Application.Certificates;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Certificates;

internal static class CertificateServiceSupport
{
    private const string BinaryContentType = "application/octet-stream";
    private static readonly Dictionary<string, string> AttachmentContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
        };

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
        if (upload.Content.Length == 0 || upload.Content.Length > CertificateAttachmentUpload.MaxSizeBytes)
        {
            throw new ArgumentException("证书附件不能为空且不能超过 20MB。", nameof(upload));
        }
        var originalName = Path.GetFileName(Required(upload.OriginalFileName, nameof(upload.OriginalFileName)));
        if (!string.Equals(originalName, upload.OriginalFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("附件文件名无效。", nameof(upload));
        }
        if (!TryGetAttachmentContentType(originalName, out var contentType))
        {
            throw new ArgumentException("证书附件类型不受支持。", nameof(upload));
        }
        await using var stream = new MemoryStream(upload.Content, writable: false);
        var storedName = await fileStore.SaveAsync(stream, originalName, cancellationToken);
        var uploadedByUserId = await db.Users.AnyAsync(item => item.Id == userId, cancellationToken) ? userId : null;
        var attachment = new Attachment
        {
            StoredName = storedName,
            OriginalFileName = originalName,
            ContentType = contentType,
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
        var contentType = TryGetAttachmentContentType(attachment.OriginalFileName, out var normalizedContentType)
            ? normalizedContentType
            : BinaryContentType;
        return new CertificateFileDto(attachment.OriginalFileName, contentType, memory.ToArray());
    }

    public static async Task RemoveAttachmentAsync(Attachment? attachment, IFileStore fileStore, CancellationToken cancellationToken)
    {
        if (attachment is null || attachment.IsDeleted) return;
        MarkAttachmentDeleted(attachment);
        await DeleteStoredFileAsync(attachment, fileStore, cancellationToken);
    }

    public static void MarkAttachmentDeleted(Attachment? attachment)
    {
        if (attachment is not null) attachment.IsDeleted = true;
    }

    public static async Task DeleteStoredFileAsync(Attachment? attachment, IFileStore fileStore, CancellationToken cancellationToken)
    {
        if (attachment is not null) await fileStore.DeleteAsync(attachment.StoredName, cancellationToken);
    }

    private static bool TryGetAttachmentContentType(string fileName, out string contentType) =>
        AttachmentContentTypes.TryGetValue(Path.GetExtension(fileName), out contentType!);
}
