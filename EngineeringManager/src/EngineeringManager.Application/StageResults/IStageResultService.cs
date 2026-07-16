namespace EngineeringManager.Application.StageResults;

public interface IStageResultService
{
    Task<StageResultDto> CreateAsync(CreateStageResultRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StageResultDto>> ListByProjectAsync(Guid? projectId, CancellationToken cancellationToken);
    Task<StageResultDto?> GetAsync(Guid stageResultId, CancellationToken cancellationToken);
}
