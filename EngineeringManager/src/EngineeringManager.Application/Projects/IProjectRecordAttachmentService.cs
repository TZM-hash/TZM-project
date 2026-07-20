namespace EngineeringManager.Application.Projects;

public interface IProjectRecordAttachmentService
{
    Task<IReadOnlyList<ProjectRecordAttachmentDto>> ListAsync(Guid projectId, ProjectRecordAttachmentType recordType, Guid recordId, CancellationToken token);
    Task<ProjectRecordAttachmentDto> UploadAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token);
    Task<ProjectRecordAttachmentFile> DownloadAsync(Guid projectId, Guid attachmentId, CancellationToken token);
    Task DeleteAsync(ProjectRecordAttachmentActor actor, Guid projectId, Guid attachmentId, CancellationToken token);
}
