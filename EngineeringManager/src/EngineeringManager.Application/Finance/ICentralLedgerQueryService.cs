using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.Finance;

public interface ICentralLedgerQueryService
{
    Task<CentralLedgerOverviewPageDto> SearchAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token);
    Task<CentralLedgerDetailsDto?> GetAsync(CentralLedgerActor actor, FinanceRecordType type, Guid id, CancellationToken token);
    Task<CentralLedgerOptionsDto> GetOptionsAsync(CentralLedgerActor actor, LedgerScope scope, CancellationToken token);
    Task<CentralLedgerMetrics> GetProjectMetricsAsync(CentralLedgerActor actor, Guid projectId, CancellationToken token);
    Task<CentralLedgerMetrics> GetPartnerMetricsAsync(CentralLedgerActor actor, Guid businessPartnerId, CancellationToken token);
}
