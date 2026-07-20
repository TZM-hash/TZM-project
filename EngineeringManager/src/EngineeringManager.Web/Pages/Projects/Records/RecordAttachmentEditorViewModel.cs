using EngineeringManager.Application.Projects;

namespace EngineeringManager.Web.Pages.Projects.Records;

public sealed record RecordAttachmentEditorViewModel(Guid ProjectId, string Section, ProjectRecordAttachmentType RecordType, Guid RecordId, string Title, IReadOnlyList<ProjectRecordAttachmentDto> Attachments);
