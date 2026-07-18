namespace EngineeringManager.Application.ConstructionCrews;

public interface IConstructionCrewService
{
    Task<IReadOnlyList<ConstructionCrewListItemDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<ConstructionCrewDetailsDto?> GetAsync(Guid crewBusinessPartnerId, bool canViewSensitive, CancellationToken cancellationToken);
    Task<ConstructionWorkerDto> AddWorkerAsync(string userId, CreateConstructionWorkerRequest request, CancellationToken cancellationToken);
    Task TransferWorkerAsync(string userId, TransferConstructionWorkerRequest request, CancellationToken cancellationToken);
}
