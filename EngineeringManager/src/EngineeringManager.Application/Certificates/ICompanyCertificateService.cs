using EngineeringManager.Application.Companies;

namespace EngineeringManager.Application.Certificates;

public interface ICompanyCertificateService
{
    Task<IReadOnlyList<CompanyCertificateItemDto>> ListAsync(CompanyActor actor, CertificateFilter filter, DateOnly today, CancellationToken cancellationToken);
    Task<CompanyCertificateItemDto> GetAsync(CompanyActor actor, Guid id, DateOnly today, CancellationToken cancellationToken);
    Task<CompanyCertificateItemDto> SaveAsync(CompanyActor actor, SaveCompanyCertificateItemRequest request, DateOnly today, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyCertificateItemDto>> SaveManyAsync(CompanyActor actor, IReadOnlyList<SaveCompanyCertificateItemRequest> requests, DateOnly today, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task DeleteAsync(CompanyActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken cancellationToken);
    Task<CertificateFileDto> DownloadAttachmentAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken);
}
