namespace EngineeringManager.Application.Finance;

public interface IFinanceReconciliationService
{
    Task<IReadOnlyList<FinanceReconciliationDto>> ListAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token);
    Task<Guid> CreateAsync(CentralLedgerActor actor, CreateFinanceReconciliationRequest request, CancellationToken token);
    Task<FinanceReconciliationDetailsDto?> GetDetailsAsync(CentralLedgerActor actor, Guid id, CancellationToken token);
    Task DeleteAsync(CentralLedgerActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token);
}
