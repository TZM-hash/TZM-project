namespace EngineeringManager.Application.EmployeeAnnualLedger;

public interface IBusinessYearService
{
    Task<BusinessYearDto> CreateAsync(CreateBusinessYearRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessYearDto>> ListAsync(CancellationToken cancellationToken);
    Task<BusinessYearDto?> GetByDateAsync(DateOnly businessDate, CancellationToken cancellationToken);
}
