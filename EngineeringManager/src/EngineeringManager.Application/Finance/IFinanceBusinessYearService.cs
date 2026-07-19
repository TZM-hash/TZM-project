namespace EngineeringManager.Application.Finance;

public interface IFinanceBusinessYearService
{
    Task<IReadOnlyList<FinanceBusinessYearDto>> ListAsync(CancellationToken token);
    Task<FinanceBusinessYearDto> CreateAsync(CentralLedgerActor actor, CreateFinanceBusinessYearRequest request, CancellationToken token);
    Task<FinanceBusinessYearDto?> ResolveAsync(DateOnly businessDate, CancellationToken token);
    Task DeleteAsync(CentralLedgerActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token);
}
