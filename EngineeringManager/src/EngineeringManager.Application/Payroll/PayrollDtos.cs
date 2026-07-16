using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.Payroll;

public sealed record CreatePayrollBatchRequest(
    string BatchNumber,
    string Name,
    PayrollBatchType BatchType,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? ProjectId,
    Guid? LegalEntityId,
    string? StageOrMilestoneName);

public sealed record PayrollCostAllocationRequest(Guid ProjectId, Guid LegalEntityId, decimal Amount);

public sealed record CreatePayrollItemRequest(
    Guid PayrollBatchId,
    Guid EmployeeId,
    PayrollItemType ItemType,
    PayrollItemNature Nature,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? ManualAmount,
    string? Description,
    IReadOnlyList<PayrollCostAllocationRequest> CostAllocations);

public sealed record RecordPayrollPaymentRequest(
    Guid PayrollBatchId,
    Guid EmployeeId,
    Guid AccountId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    PayrollPayeeType PayeeType,
    string PayeeName,
    Guid? PayeeBusinessPartnerId,
    string? Notes);

public sealed record PayrollBatchDto(
    Guid Id,
    string BatchNumber,
    string Name,
    PayrollBatchType BatchType,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? ProjectId,
    Guid? LegalEntityId,
    PayrollBatchStatus Status);

public sealed record PayrollItemDto(
    Guid Id,
    Guid EmployeeId,
    PayrollItemType ItemType,
    PayrollItemNature Nature,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    string? Description);

public sealed record EmployeePayrollSummaryDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    decimal GrossEarnings,
    decimal DeductionAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    bool HasOverpaymentRisk,
    bool HasDeductionRisk);

public sealed record PayrollBatchSummaryDto(
    Guid BatchId,
    decimal GrossEarnings,
    decimal DeductionAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    bool HasOverpaymentRisk,
    bool HasDeductionRisk,
    IReadOnlyList<EmployeePayrollSummaryDto> EmployeeSummaries);

public sealed record PayrollBatchListItemDto(PayrollBatchDto Batch, PayrollBatchSummaryDto Summary);

public sealed record PayrollOverviewDto(
    decimal GrossEarnings,
    decimal DeductionAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    bool HasOverpaymentRisk,
    bool HasDeductionRisk,
    IReadOnlyList<PayrollBatchListItemDto> Batches);
