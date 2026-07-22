using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Application.EmployeeLedger;

public sealed record ExpenseAttachmentUpload(string OriginalFileName, string ContentType, byte[] Content);

public sealed record CreateExpenseRequest(
    Guid EmployeeId,
    Guid? ProjectId,
    Guid? DepartmentId,
    Guid LegalEntityId,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string? Description,
    decimal AdjustmentAmount = 0m,
    string? ReceiptNumber = null,
    ExpenseAttachmentUpload? Attachment = null);

public sealed record RecordExpensePaymentRequest(
    Guid ExpenseRecordId,
    Guid AccountId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod PaymentMethod,
    EmployeeLedgerRecordKind RecordKind,
    string? Notes);

public sealed record UpdateExpenseRequest(
    Guid Id,
    Guid ConcurrencyStamp,
    DateOnly ExpenseDate,
    decimal Amount,
    Guid? ProjectId,
    string? ReceiptNumber,
    ExpenseAttachmentUpload? Attachment,
    string? Description,
    string Reason,
    string? UserId = null);

public sealed record EmployeeExpenseDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly ExpenseDate,
    decimal Amount,
    Guid? ProjectId,
    string? ProjectName,
    string? ReceiptNumber,
    Guid? AttachmentId,
    string? AttachmentFileName,
    string? Description,
    Guid ConcurrencyStamp);

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

public sealed record EmployeeOtherPayableDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly EntryDate,
    decimal Amount,
    EmployeeLedgerEntryType EntryType,
    Guid LegalEntityId,
    string LegalEntityName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? AttachmentId,
    string? AttachmentFileName,
    string? Description,
    Guid ConcurrencyStamp);

public sealed record UpdateEmployeeOtherPayableRequest(
    Guid Id,
    Guid ConcurrencyStamp,
    DateOnly EntryDate,
    decimal Amount,
    EmployeeLedgerEntryType EntryType,
    Guid LegalEntityId,
    Guid? ProjectId,
    string? Description,
    string Reason,
    string? UserId = null);

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
