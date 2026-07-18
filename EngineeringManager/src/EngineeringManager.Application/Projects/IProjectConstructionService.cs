namespace EngineeringManager.Application.Projects;

public interface IProjectConstructionService
{
    Task<ProjectConstructionWorkspaceDto> GetWorkspaceAsync(Guid projectId, DateOnly today, CancellationToken token);
    Task<ProjectConstructionRecordDto> SaveAsync(ProjectConstructionActor actor, SaveProjectConstructionRecordRequest request, DateOnly today, CancellationToken token);
    Task<ProjectConstructionOptionDto> CreateEquipmentAsync(ProjectConstructionActor actor, CreateProjectEquipmentRequest request, CancellationToken token);
    Task<ProjectConstructionOptionDto> CreateCrewAsync(ProjectConstructionActor actor, CreateProjectCrewRequest request, CancellationToken token);
}
