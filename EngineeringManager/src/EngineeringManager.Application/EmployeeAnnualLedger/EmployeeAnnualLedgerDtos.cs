using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Application.EmployeeAnnualLedger;

public sealed record CreateBusinessYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record BusinessYearDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, Guid ConcurrencyStamp);

public sealed record CreateEmployeeWageEntryRequest(
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly StartDate,
    DateOnly EndDate,
    EmployeeWageCategory WageCategory,
    EmployeeWageCalculationMethod CalculationMethod,
    PayrollItemNature Nature,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal? ManualAmount,
    Guid? LegalEntityId,
    Guid? ProjectId,
    Guid? LaborBusinessPartnerId,
    decimal AdjustmentAmount,
    string? Notes);

public sealed record EmployeeWageEntryDto(
    Guid Id,
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly StartDate,
    DateOnly EndDate,
    EmployeeWageCategory WageCategory,
    EmployeeWageCalculationMethod CalculationMethod,
    PayrollItemNature Nature,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal AutomaticAmount,
    Guid? LegalEntityId,
    Guid? ProjectId,
    Guid? LaborBusinessPartnerId,
    decimal AdjustmentAmount,
    decimal FinalAmount,
    string? Notes,
    bool IsUnassignedMigrantWage);

public sealed record CreateEmployeeFinancialAdjustmentRequest(
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly AdjustmentDate,
    decimal Amount,
    EmployeeFinancialAdjustmentType AdjustmentType,
    string Notes);

public sealed record EmployeeFinancialAdjustmentDto(
    Guid Id,
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly AdjustmentDate,
    decimal Amount,
    EmployeeFinancialAdjustmentType AdjustmentType,
    string Notes,
    Guid? ReversalOfId);

public sealed record RecordEmployeeReceiptRequest(
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly ReceiptDate,
    EmployeeReceiptType ReceiptType,
    decimal Amount,
    Guid PaymentLegalEntityId,
    Guid AccountId,
    EngineeringManager.Domain.Finance.PaymentMethod PaymentMethod,
    string ActualRecipientName,
    Guid? ProjectId,
    Guid? LaborBusinessPartnerId,
    string? Notes);

public sealed record EmployeeReceiptDto(
    Guid Id,
    Guid EmployeeId,
    Guid BusinessYearId,
    DateOnly ReceiptDate,
    EmployeeReceiptType ReceiptType,
    decimal Amount,
    Guid PaymentLegalEntityId,
    Guid AccountId,
    EngineeringManager.Domain.Finance.PaymentMethod PaymentMethod,
    string ActualRecipientName,
    Guid? ProjectId,
    Guid? LaborBusinessPartnerId,
    string? Notes);

public sealed record EmployeeAnnualLedgerDto(
    Guid EmployeeId,
    Guid BusinessYearId,
    EmployeeAnnualLedgerSummary Summary,
    IReadOnlyList<EmployeeAnnualLedgerPayableLineDto> PayableLines,
    IReadOnlyList<EmployeeAnnualLedgerReceiptLineDto> ReceiptLines);

public sealed record EmployeeAnnualLedgerPayableLineDto(
    Guid Id,
    AnnualLedgerEntryCategory Category,
    string SourceType,
    DateOnly EntryDate,
    DateOnly? EndDate,
    decimal Amount,
    string? Description,
    bool IsUnassigned);

public sealed record EmployeeAnnualLedgerReceiptLineDto(
    Guid Id,
    string SourceType,
    DateOnly ReceiptDate,
    decimal Amount,
    string? Recipient,
    string? Notes,
    Guid? PayrollBatchId = null,
    Guid? PayrollPaymentId = null);
