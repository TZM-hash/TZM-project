namespace EngineeringManager.Application.Payroll;

public interface IPayrollService
{
    Task<PayrollDisbursementBatchDetailsDto> SaveDisbursementBatchAsync(string userId, SavePayrollDisbursementBatchRequest request, CancellationToken cancellationToken);
    Task<PayrollDisbursementBatchDetailsDto?> GetDisbursementBatchAsync(Guid batchId, CancellationToken cancellationToken);
    Task<PayrollDisbursementOverviewDto> GetDisbursementOverviewAsync(CancellationToken cancellationToken);
    Task<PayrollBatchDto> CreateBatchAsync(CreatePayrollBatchRequest request, CancellationToken cancellationToken);
    Task<PayrollItemDto> AddItemAsync(CreatePayrollItemRequest request, CancellationToken cancellationToken);
    Task<Guid> RecordPaymentAsync(RecordPayrollPaymentRequest request, CancellationToken cancellationToken);
    Task<PayrollBatchSummaryDto> GetBatchSummaryAsync(Guid batchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PayrollBatchDto>> ListBatchesAsync(CancellationToken cancellationToken);
    Task<PayrollOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}
