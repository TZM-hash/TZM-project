namespace EngineeringManager.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, bool canViewSensitiveData, CancellationToken cancellationToken) => ListAsync(search, cancellationToken);
    Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken);
}
