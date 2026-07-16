namespace EngineeringManager.Application.EmployeeLedger;

public interface IEmployeeLedgerService
{
    Task<Guid> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordExpensePaymentAsync(RecordExpensePaymentRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordAdvanceAsync(RecordEmployeeAdvanceRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateOtherPayableAsync(CreateEmployeeOtherPayableRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordOtherPaymentAsync(RecordEmployeeOtherPaymentRequest request, CancellationToken cancellationToken);
    Task<EmployeeLedgerSummaryDto> GetEmployeeSummaryAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeLedgerOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}
