using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.Finance;

public sealed record CentralLedgerActor(
    string UserId,
    string? UserName,
    IReadOnlySet<Guid> LegalEntityIds,
    IReadOnlySet<Guid> ProjectIds,
    bool CanManageExternal,
    bool CanManageInternal,
    bool CanManageYears,
    bool CanReconcile);

public sealed record CreateSettlementRequest(
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerSettlementState SettlementState,
    LedgerSourceType SourceType,
    Guid? SourceId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    Guid? ContractLineItemId,
    DateOnly BusinessDate,
    decimal OriginalAmount,
    decimal OriginalInvoiceAmount,
    string? Notes,
    DateOnly? DueDate = null);

public sealed record FinalizeSettlementRequest(
    Guid SettlementId,
    DateOnly BusinessDate,
    decimal FinalAmount,
    decimal FinalInvoiceAmount,
    string Reason,
    Guid ConcurrencyStamp);

public sealed record AddFinanceDeductionRequest(
    Guid SettlementId,
    DateOnly BusinessDate,
    decimal Amount,
    bool ReduceInvoiceAmount,
    string Reason,
    Guid SettlementConcurrencyStamp);

public sealed record FinanceAllocationRequest(
    Guid SettlementId,
    decimal Amount,
    int AllocationOrder);

public sealed record CreateFinanceInvoiceRequest(
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerSourceType SourceType,
    Guid? SourceId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal Amount,
    decimal? NetAmount,
    decimal? TaxAmount,
    decimal? TaxRate,
    string? Notes,
    IReadOnlyList<FinanceAllocationRequest> Allocations,
    bool AutoAllocate = false,
    Guid? ProjectTaxConfigurationId = null,
    string? InvoiceType = null,
    LedgerRecordStatus Status = LedgerRecordStatus.Active,
    Guid? ProjectId = null,
    Guid? ContractId = null);

public sealed record CreateFinanceCashRequest(
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerCashType CashType,
    LedgerSourceType SourceType,
    Guid? SourceId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? AccountId,
    Guid? CounterAccountId,
    DateOnly BusinessDate,
    decimal Amount,
    string? PaymentMethod,
    string? Notes,
    IReadOnlyList<FinanceAllocationRequest> Allocations,
    bool AutoAllocate = false,
    Guid? ProjectId = null,
    Guid? ContractId = null,
    Guid? EntryId = null);

public sealed record ReplaceInvoiceAllocationsRequest(
    Guid InvoiceId,
    IReadOnlyList<FinanceAllocationRequest> Allocations,
    Guid ConcurrencyStamp,
    string Reason);

public sealed record ReplaceCashAllocationsRequest(
    Guid CashEntryId,
    IReadOnlyList<FinanceAllocationRequest> Allocations,
    Guid ConcurrencyStamp,
    string Reason);

public sealed record DeleteFinanceRecordRequest(
    FinanceRecordType RecordType,
    Guid RecordId,
    Guid ConcurrencyStamp,
    string Reason,
    string EntryPoint);

public sealed record CentralLedgerQuery(
    LedgerScope Scope,
    LedgerDirection? Direction = null,
    Guid? FinanceBusinessYearId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid? CounterLegalEntityId = null,
    Guid? ProjectId = null,
    Guid? ContractId = null,
    Guid? ContractLineItemId = null,
    LedgerSettlementState? SettlementState = null,
    LedgerRecordStatus? RecordStatus = null,
    LedgerAllocationStatus? InvoiceAllocationStatus = null,
    LedgerAllocationStatus? CashAllocationStatus = null,
    bool? HasAdvanceInvoiceCash = null,
    bool? HasOverSettlementCash = null,
    bool? HasOverInvoiced = null,
    string? Search = null,
    string? SortKey = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20);

public sealed record CentralLedgerRowDto(
    Guid SettlementId,
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerSettlementState SettlementState,
    DateOnly BusinessDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? CounterLegalEntityId,
    string? CounterLegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? ContractId,
    string? ContractName,
    CentralLedgerMetrics Metrics,
    LedgerAllocationStatus InvoiceAllocationStatus,
    LedgerAllocationStatus CashAllocationStatus,
    Guid ConcurrencyStamp)
{
    public LedgerSourceType SourceType { get; init; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; init; }
    public string? SourceUrl { get; init; }
}

public sealed record CentralLedgerOverviewPageDto(
    IReadOnlyList<CentralLedgerRowDto> Rows,
    CentralLedgerMetrics Totals,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<Guid> MatchingSettlementIds,
    IReadOnlyList<CentralLedgerUnallocatedCashDto>? UnallocatedCash = null,
    IReadOnlyList<CentralLedgerPayrollPaymentDto>? PayrollPayments = null,
    decimal PayrollPaymentTotal = 0m,
    IReadOnlyList<CentralLedgerInvoiceDto>? Invoices = null,
    IReadOnlyList<CentralLedgerCashEntryDto>? CashEntries = null,
    IReadOnlyList<CentralLedgerDeductionDto>? Deductions = null,
    IReadOnlyList<CentralLedgerAuditDto>? AuditEntries = null,
    CentralLedgerMetrics? ReceivableTotals = null,
    CentralLedgerMetrics? PayableTotals = null);

public sealed record PartnerLedgerSummaryDto(
    Guid BusinessPartnerId,
    CentralLedgerMetrics Receivable,
    CentralLedgerMetrics Payable)
{
    public static PartnerLedgerSummaryDto Empty(Guid businessPartnerId) =>
        new(businessPartnerId, CentralLedgerMetrics.Zero, CentralLedgerMetrics.Zero);
}

public sealed record CentralLedgerUnallocatedCashDto(
    Guid CashEntryId,
    LedgerDirection Direction,
    DateOnly BusinessDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? ContractId,
    string? ContractName,
    Guid? AccountId,
    string? AccountName,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    string? PaymentMethod,
    Guid ConcurrencyStamp,
    Guid? CounterLegalEntityId = null,
    string? CounterLegalEntityName = null);

public sealed record CentralLedgerPayrollPaymentDto(
    Guid BatchId,
    string BatchNumber,
    string BatchName,
    DateOnly PaymentDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid AccountId,
    string AccountName,
    decimal EmployeeAmount,
    decimal CrewAmount,
    decimal ActualAmount,
    PayrollBatchStatus Status);

public sealed record CentralLedgerInvoiceDto(
    Guid Id,
    LedgerScope Scope,
    LedgerDirection Direction,
    DateOnly InvoiceDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? CounterLegalEntityId,
    string? CounterLegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? ContractId,
    string? ContractName,
    string InvoiceNumber,
    string? InvoiceType,
    decimal Amount,
    decimal? NetAmount,
    decimal? TaxAmount,
    decimal? TaxRate,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    LedgerRecordStatus Status,
    LedgerSourceType SourceType,
    Guid? SourceId,
    string? SourceUrl,
    string? Notes,
    Guid ConcurrencyStamp);

public sealed record CentralLedgerCashEntryDto(
    Guid Id,
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerCashType CashType,
    bool IsReversal,
    DateOnly BusinessDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? CounterLegalEntityId,
    string? CounterLegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? ContractId,
    string? ContractName,
    Guid? AccountId,
    string? AccountName,
    Guid? CounterAccountId,
    string? CounterAccountName,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    string? PaymentMethod,
    LedgerRecordStatus Status,
    LedgerSourceType SourceType,
    Guid? SourceId,
    string? SourceUrl,
    string? Notes,
    Guid ConcurrencyStamp);

public sealed record CentralLedgerDeductionDto(
    Guid Id,
    Guid SettlementId,
    LedgerScope Scope,
    LedgerDirection Direction,
    DateOnly BusinessDate,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? CounterLegalEntityId,
    string? CounterLegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    decimal Amount,
    bool ReduceInvoiceAmount,
    string Reason,
    LedgerRecordStatus Status,
    LedgerSourceType SourceType,
    Guid? SourceId,
    Guid ConcurrencyStamp);

public sealed record CentralLedgerAuditDto(
    string RecordId,
    string EntityType,
    string Action,
    DateTimeOffset OccurredAt,
    string? UserName,
    string? Reason,
    bool IsDeletion);

public sealed record CentralLedgerDetailsDto(
    FinanceRecordType RecordType,
    Guid Id,
    LedgerScope Scope,
    LedgerDirection Direction,
    string HeaderJson,
    CentralLedgerMetrics Metrics,
    IReadOnlyList<FinanceAllocationDto> Allocations,
    Guid ConcurrencyStamp)
{
    public LedgerSourceType SourceType { get; init; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; init; }
    public string? SourceUrl { get; init; }
    public string? SourceLabel { get; init; }
}

public sealed record UpdateSettlementRequest(
    Guid SettlementId,
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerSettlementState SettlementState,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    DateOnly BusinessDate,
    decimal OriginalAmount,
    decimal OriginalInvoiceAmount,
    string? Notes,
    string Reason,
    Guid ConcurrencyStamp,
    DateOnly? DueDate = null);

public sealed record UpdateFinanceInvoiceRequest(
    Guid InvoiceId,
    LedgerScope Scope,
    LedgerDirection Direction,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal Amount,
    decimal? NetAmount,
    decimal? TaxAmount,
    decimal? TaxRate,
    string? InvoiceType,
    string? Notes,
    string Reason,
    Guid ConcurrencyStamp);

public sealed record UpdateFinanceCashRequest(
    Guid CashEntryId,
    LedgerScope Scope,
    LedgerDirection Direction,
    LedgerCashType CashType,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? CounterLegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    Guid? AccountId,
    Guid? CounterAccountId,
    DateOnly BusinessDate,
    decimal Amount,
    string? PaymentMethod,
    string? Notes,
    string Reason,
    Guid ConcurrencyStamp);

public sealed record FinanceAllocationDto(
    Guid Id,
    Guid SettlementId,
    Guid? ProjectId,
    Guid? ContractId,
    Guid? ContractLineItemId,
    decimal Amount,
    int AllocationOrder);

public sealed record CentralLedgerOptionDto(Guid Id, string Label, Guid? ParentId = null, string? Kind = null);

public sealed record CentralLedgerOptionsDto(
    IReadOnlyList<CentralLedgerOptionDto> LegalEntities,
    IReadOnlyList<CentralLedgerOptionDto> CounterLegalEntities,
    IReadOnlyList<CentralLedgerOptionDto> Projects,
    IReadOnlyList<CentralLedgerOptionDto> Contracts,
    IReadOnlyList<CentralLedgerOptionDto> ContractLineItems,
    IReadOnlyList<CentralLedgerOptionDto> BusinessPartners,
    IReadOnlyList<CentralLedgerOptionDto> Crews,
    IReadOnlyList<CentralLedgerOptionDto> Accounts,
    IReadOnlyList<CentralLedgerOptionDto> FinanceBusinessYears);

public sealed record CreateCrewPayableRequest(
    Guid CrewBusinessPartnerId,
    Guid LegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    DateOnly BusinessDate,
    LedgerSettlementState SettlementState,
    decimal Amount,
    decimal InvoiceAmount,
    string? Notes);

public sealed record CreatePartnerPayableRequest(
    Guid BusinessPartnerId,
    Guid LegalEntityId,
    Guid? ProjectId,
    Guid? ContractId,
    DateOnly BusinessDate,
    LedgerSettlementState SettlementState,
    decimal Amount,
    decimal InvoiceAmount,
    string? Notes);

public sealed record CreateFinanceBusinessYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record FinanceBusinessYearDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int RecordCount,
    Guid ConcurrencyStamp);

public sealed record CreateFinanceReconciliationRequest(
    LedgerScope Scope,
    FinanceReconciliationScope ReconciliationScope,
    Guid? FinanceBusinessYearId,
    Guid? LegalEntityId,
    Guid? BusinessPartnerId,
    DateOnly? StartDate,
    DateOnly AsOfDate,
    CentralLedgerQuery Query);

public sealed record FinanceReconciliationDto(
    Guid Id,
    LedgerScope Scope,
    FinanceReconciliationScope ReconciliationScope,
    DateOnly AsOfDate,
    int Version,
    CentralLedgerMetrics SnapshotMetrics,
    DateTimeOffset CreatedAt,
    string? CreatedByUserName,
    Guid ConcurrencyStamp);

public sealed record FinanceReconciliationDifferenceDto(
    Guid SettlementId,
    CentralLedgerMetrics SnapshotMetrics,
    CentralLedgerMetrics CurrentMetrics,
    CentralLedgerMetrics Difference,
    bool WasDeleted);

public sealed record FinanceReconciliationDetailsDto(
    FinanceReconciliationDto Reconciliation,
    CentralLedgerMetrics CurrentMetrics,
    CentralLedgerMetrics Difference,
    IReadOnlyList<FinanceReconciliationDifferenceDto> Lines);
