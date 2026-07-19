namespace EngineeringManager.Application.ConstructionCrews;

public interface IConstructionCrewService
{
    Task<IReadOnlyList<ConstructionCrewListItemDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConstructionCrewListItemDto>> ListAsync(bool includeInactive, string? search, CancellationToken cancellationToken) => ListAsync(includeInactive, cancellationToken);
    Task<IReadOnlyList<ConstructionCrewListItemDto>> ListAsync(bool includeInactive, string? search, bool canViewSensitiveData, CancellationToken cancellationToken) => ListAsync(includeInactive, search, cancellationToken);
    Task<ConstructionCrewDetailsDto?> GetAsync(Guid crewBusinessPartnerId, bool canViewSensitive, CancellationToken cancellationToken);
    Task<ConstructionWorkerDto> AddWorkerAsync(string userId, CreateConstructionWorkerRequest request, CancellationToken cancellationToken);
    Task TransferWorkerAsync(string userId, TransferConstructionWorkerRequest request, CancellationToken cancellationToken);
}
