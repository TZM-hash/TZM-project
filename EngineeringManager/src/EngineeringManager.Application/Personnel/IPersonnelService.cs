namespace EngineeringManager.Application.Personnel;

public interface IPersonnelService
{
    Task<PersonnelDetailsDto> CreateAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken);
    Task<PersonnelDetailsDto?> GetAsync(Guid personId, DateOnly? asOf, bool canViewSensitiveData, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonnelListItemDto>> ListAsync(PersonnelListQuery query, bool canViewSensitiveData, CancellationToken cancellationToken);
    Task<PersonnelDetailsDto> SavePublicDataAsync(string userId, SavePersonRequest request, CancellationToken cancellationToken);
    Task<PersonnelAffiliationDto> SaveAffiliationAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken);
    Task<PersonnelDetailsDto> SwitchScopeAsync(string userId, SwitchPersonnelScopeRequest request, CancellationToken cancellationToken);
    Task<PersonnelOptionSetDto> GetOptionsAsync(CancellationToken cancellationToken);
    Task<Guid?> ResolvePersonIdForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
}
