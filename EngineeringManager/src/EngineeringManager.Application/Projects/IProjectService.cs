using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public interface IProjectService
{
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken);

    Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(
        string? search,
        ProjectStage? stage,
        CancellationToken cancellationToken);

    Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
}
