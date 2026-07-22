namespace EngineeringManager.Application.EmployeeAnnualLedger;

public interface IEmployeeAnnualLedgerService
{
    Task<EmployeeWageEntryDto> AddWageEntryAsync(CreateEmployeeWageEntryRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeWageEntryDto>> GetWageEntriesAsync(Guid employeeId, Guid businessYearId, CancellationToken cancellationToken);
    Task<EmployeeWageEntryDto> UpdateWageEntryAsync(UpdateEmployeeWageEntryRequest request, CancellationToken cancellationToken);
    Task<EmployeeReceiptDto> RecordReceiptAsync(RecordEmployeeReceiptRequest request, CancellationToken cancellationToken);
    Task<EmployeeFinancialAdjustmentDto> AddAdjustmentAsync(CreateEmployeeFinancialAdjustmentRequest request, CancellationToken cancellationToken);
    Task<EmployeeFinancialAdjustmentDto> ReverseAdjustmentAsync(Guid adjustmentId, DateOnly reversalDate, string notes, CancellationToken cancellationToken);
    Task<EmployeeAnnualLedgerDto> GetAnnualLedgerAsync(Guid employeeId, Guid businessYearId, CancellationToken cancellationToken);
}
