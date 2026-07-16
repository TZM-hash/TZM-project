using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.Finance;

public enum FinanceEntryKind
{
    Receivable = 1,
    Collection = 2,
    RefundOrCollectionReversal = 3,
    Payable = 4,
    Payment = 5,
    Deduction = 6,
    PaymentReversal = 7,
    Transfer = 8,
    Invoice = 9
}

public sealed record CreateFinancialAccountRequest(
    Guid LegalEntityId,
    string AccountName,
    string? AccountNumber,
    string? BankName,
    FinancialAccountType AccountType,
    decimal OpeningBalance);

public sealed record FinancialAccountDto(
    Guid Id,
    Guid LegalEntityId,
    string LegalEntityName,
    string AccountName,
    string? AccountNumber,
    string? BankName,
    FinancialAccountType AccountType,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsActive);

public sealed record ProjectFinanceListItemDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    FinanceProjectSummaryDto Summary);

public sealed record FinanceOverviewDto(
    IReadOnlyList<ProjectFinanceListItemDto> Projects,
    FinanceProjectSummaryDto Total);

public sealed record FinanceOptionDto(Guid Id, string Label, Guid? ParentId = null);

public sealed record FinanceEntryOptionsDto(
    IReadOnlyList<FinanceOptionDto> Projects,
    IReadOnlyList<FinanceOptionDto> Contracts,
    IReadOnlyList<FinanceOptionDto> LegalEntities,
    IReadOnlyList<FinanceOptionDto> BusinessPartners,
    IReadOnlyList<FinanceOptionDto> Accounts,
    IReadOnlyList<FinanceOptionDto> Receivables,
    IReadOnlyList<FinanceOptionDto> Payables,
    IReadOnlyList<FinanceOptionDto> Collections,
    IReadOnlyList<FinanceOptionDto> Payments);

public sealed record CreateReceivableRequest(
    Guid ProjectId,
    Guid? ContractId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    ReceivableSourceType SourceType,
    DateOnly EntryDate,
    DateOnly? DueDate,
    decimal Amount,
    string? Description);

public sealed record RecordCollectionRequest(
    Guid? ReceivableEntryId,
    Guid ProjectId,
    Guid? ContractId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    Guid AccountId,
    DateOnly CollectionDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Notes);

public sealed record RecordRefundRequest(
    Guid? CollectionEntryId,
    Guid? ReceivableEntryId,
    Guid AccountId,
    DateOnly EntryDate,
    decimal Amount,
    FinancialAdjustmentType AdjustmentType,
    string Reason);

public sealed record CreatePayableRequest(
    Guid ProjectId,
    Guid? ContractId,
    Guid LegalEntityId,
    Guid BusinessPartnerId,
    PayableSourceType SourceType,
    DateOnly EntryDate,
    DateOnly? DueDate,
    decimal Amount,
    string? Description);

public sealed record RecordPaymentRequest(
    Guid? PayableEntryId,
    Guid ProjectId,
    Guid? ContractId,
    Guid LegalEntityId,
    Guid BusinessPartnerId,
    Guid AccountId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Notes);

public sealed record CreateDeductionRequest(
    Guid PayableEntryId,
    Guid ProjectId,
    Guid LegalEntityId,
    Guid BusinessPartnerId,
    DateOnly EntryDate,
    decimal Amount,
    string Reason);

public sealed record RecordPaymentReversalRequest(
    Guid PaymentEntryId,
    Guid AccountId,
    DateOnly EntryDate,
    decimal Amount,
    FinancialAdjustmentType AdjustmentType,
    string Reason);

public sealed record CreateAccountTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    DateOnly TransferDate,
    decimal Amount,
    string? Description);

public sealed record InvoiceAllocationRequest(Guid TargetId, decimal AllocatedAmount);

public sealed record CreateInvoiceRequest(
    Guid ProjectId,
    Guid? ContractId,
    Guid LegalEntityId,
    Guid? BusinessPartnerId,
    InvoiceDirection Direction,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string? InvoiceType,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    InvoiceStatus Status,
    IReadOnlyList<InvoiceAllocationRequest> ReceivableAllocations,
    IReadOnlyList<InvoiceAllocationRequest> LineItemAllocations);

public sealed record FinanceSummaryFilter(
    Guid ProjectId,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    DateOnly? CutoffDate = null);

public sealed record FinanceProjectSummaryDto(
    Guid ProjectId,
    decimal ReceivableAmount,
    decimal CollectedAmount,
    decimal UncollectedAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal DeductionAmount,
    decimal UnpaidAmount,
    decimal OutputInvoiceAmount,
    decimal UninvoicedAmount,
    decimal InputInvoiceAmount,
    bool HasCollectionRisk,
    bool HasPaymentRisk);
