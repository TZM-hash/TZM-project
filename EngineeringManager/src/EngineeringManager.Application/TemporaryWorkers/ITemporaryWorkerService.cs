namespace EngineeringManager.Application.TemporaryWorkers;

public interface ITemporaryWorkerService
{
    Task<IReadOnlyList<TemporaryWorkerDto>> ListAsync(bool includeInactive, bool canViewSensitive, CancellationToken cancellationToken);
    Task<TemporaryWorkerDto?> GetAsync(Guid id, bool canViewSensitive, CancellationToken cancellationToken);
    Task<TemporaryWorkerDto> CreateAsync(string userId, CreateTemporaryWorkerRequest request, CancellationToken cancellationToken);
    Task<TemporaryWorkerDto> UpdateAsync(string userId, UpdateTemporaryWorkerRequest request, CancellationToken cancellationToken);
    Task LinkConvertedEmployeeAsync(string userId, Guid temporaryWorkerId, Guid employeeId, string reason, CancellationToken cancellationToken);
}
