namespace EngineeringManager.Application.Finance;

public interface IFinanceLedgerService
{
    Task<Guid> CreateAccountAsync(CreateFinancialAccountRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken);
    Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
    Task<FinanceOverviewPageDto> SearchOverviewAsync(FinanceOverviewQuery query, CancellationToken cancellationToken);
    Task<FinanceEntryOptionsDto> GetEntryOptionsAsync(CancellationToken cancellationToken);
    Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken);
    Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken);
    Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken);
    Task<Guid> TransferAsync(CreateAccountTransferRequest request, CancellationToken cancellationToken);
    Task<Guid> AddInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken);
    Task UpdateReceivableAsync(FinanceRecordActor actor, UpdateReceivableRequest request, CancellationToken cancellationToken);
    Task UpdateCollectionAsync(FinanceRecordActor actor, UpdateCollectionRequest request, CancellationToken cancellationToken);
    Task UpdateInvoiceAsync(FinanceRecordActor actor, UpdateInvoiceRequest request, CancellationToken cancellationToken);
    Task UpdatePayableAsync(FinanceRecordActor actor, UpdatePayableRequest request, CancellationToken cancellationToken);
    Task UpdatePaymentAsync(FinanceRecordActor actor, UpdatePaymentRequest request, CancellationToken cancellationToken);
    Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken);
    Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken);
}
