namespace EngineeringManager.Application.Projects;

public interface IProjectWorkspaceService
{
    Task<ProjectWorkspaceDto?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectEditOptionsDto> GetEditOptionsAsync(CancellationToken cancellationToken);

    Task<ProjectWorkspaceDto> UpdateAsync(
        ProjectWorkspaceActor actor,
        UpdateProjectRequest request,
        CancellationToken cancellationToken);
}
