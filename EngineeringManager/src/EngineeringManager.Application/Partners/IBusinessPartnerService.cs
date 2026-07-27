using EngineeringManager.Domain.Partners;

namespace EngineeringManager.Application.Partners;

public interface IBusinessPartnerService
{
    Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken);
    Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken);
    Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken);
    Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessPartnerDto>> ListForManagementAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) =>
        ListAsync(search, role, cancellationToken);
    Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken);
}
