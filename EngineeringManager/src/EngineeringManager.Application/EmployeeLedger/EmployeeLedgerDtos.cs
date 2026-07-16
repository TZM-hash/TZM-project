using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.EmployeeLedger;

public sealed record CreateExpenseRequest(
    Guid EmployeeId,
    Guid? ProjectId,
    Guid? DepartmentId,
    Guid LegalEntityId,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string? Description);

public sealed record RecordExpensePaymentRequest(
    Guid ExpenseRecordId,
    Guid AccountId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    EmployeeLedgerRecordKind RecordKind,
    string? Notes);

public sealed record RecordEmployeeAdvanceRequest(
    Guid EmployeeId,
    Guid? ProjectId,
    Guid LegalEntityId,
    Guid? AccountId,
    DateOnly EntryDate,
    decimal Amount,
    EmployeeAdvanceAction Action,
    string? Description);

public sealed record CreateEmployeeOtherPayableRequest(
    Guid EmployeeId,
    Guid? ProjectId,
    Guid LegalEntityId,
    DateOnly EntryDate,
    decimal Amount,
    EmployeeLedgerEntryType EntryType,
    string? Description);

public sealed record RecordEmployeeOtherPaymentRequest(
    Guid PayableEntryId,
    Guid AccountId,
    DateOnly EntryDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    EmployeeLedgerRecordKind RecordKind,
    string? Description);

public sealed record EmployeeLedgerSummaryDto(
    Guid EmployeeId,
    decimal ExpensePayableAmount,
    decimal ExpensePaidAmount,
    decimal ExpenseUnpaidAmount,
    decimal AdvanceOutstandingAmount,
    decimal OtherPayableAmount,
    decimal OtherPaidAmount,
    decimal OtherUnpaidAmount,
    bool HasExpenseOverpaymentRisk,
    bool HasOtherOverpaymentRisk,
    bool HasAdvanceOverSettlementRisk);

public sealed record EmployeeLedgerListItemDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    EmployeeLedgerSummaryDto Summary);

public sealed record EmployeeLedgerOverviewDto(
    decimal ExpensePayableAmount,
    decimal ExpensePaidAmount,
    decimal ExpenseUnpaidAmount,
    decimal AdvanceOutstandingAmount,
    decimal OtherPayableAmount,
    decimal OtherPaidAmount,
    decimal OtherUnpaidAmount,
    bool HasRisk,
    IReadOnlyList<EmployeeLedgerListItemDto> EmployeeSummaries);
