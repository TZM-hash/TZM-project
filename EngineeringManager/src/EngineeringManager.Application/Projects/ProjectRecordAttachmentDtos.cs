namespace EngineeringManager.Application.Projects;

public enum ProjectRecordAttachmentType { Quantity = 1, Settlement = 2, Invoice = 3, Cash = 4, Construction = 5 }

public sealed record ProjectRecordAttachmentActor(string UserId, bool CanManage);
public sealed record ProjectRecordAttachmentUpload(Guid ProjectId, ProjectRecordAttachmentType RecordType, Guid RecordId, string OriginalFileName, string ContentType, byte[] Content, string? Description = null);
public sealed record ProjectRecordAttachmentDto(Guid Id, Guid ProjectId, ProjectRecordAttachmentType RecordType, Guid RecordId, string OriginalFileName, string ContentType, long SizeBytes, string? Description, DateTimeOffset UploadedAt);
public sealed record ProjectRecordAttachmentFile(string OriginalFileName, string ContentType, Stream Content);
