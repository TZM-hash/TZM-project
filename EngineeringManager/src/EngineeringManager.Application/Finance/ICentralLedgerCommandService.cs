namespace EngineeringManager.Application.Finance;

public interface ICentralLedgerCommandService
{
    Task<Guid> CreateSettlementAsync(CentralLedgerActor actor, CreateSettlementRequest request, CancellationToken token);
    Task FinalizeSettlementAsync(CentralLedgerActor actor, FinalizeSettlementRequest request, CancellationToken token);
    Task<Guid> AddDeductionAsync(CentralLedgerActor actor, AddFinanceDeductionRequest request, CancellationToken token);
    Task<Guid> CreateInvoiceAsync(CentralLedgerActor actor, CreateFinanceInvoiceRequest request, CancellationToken token);
    Task<Guid> CreateCashAsync(CentralLedgerActor actor, CreateFinanceCashRequest request, CancellationToken token);
    Task ReplaceInvoiceAllocationsAsync(CentralLedgerActor actor, ReplaceInvoiceAllocationsRequest request, CancellationToken token);
    Task ReplaceCashAllocationsAsync(CentralLedgerActor actor, ReplaceCashAllocationsRequest request, CancellationToken token);
    Task DeleteAsync(CentralLedgerActor actor, DeleteFinanceRecordRequest request, CancellationToken token);
}
