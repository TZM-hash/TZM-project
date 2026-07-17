namespace EngineeringManager.Application.Certificates;

public interface IEmployeeCertificateService
{
    Task<IReadOnlyList<EmployeeCertificateDto>> ListAsync(CertificateFilter filter, DateOnly today, CancellationToken cancellationToken);
    Task<EmployeeCertificateDto> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken);
    Task<EmployeeCertificateDto> SaveAsync(string userId, bool canManage, SaveEmployeeCertificateRequest request, DateOnly today, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, bool canManage, Guid id, Guid concurrencyStamp, string reason, CancellationToken cancellationToken);
    Task<CertificateFileDto> DownloadAttachmentAsync(Guid id, CancellationToken cancellationToken);
}
