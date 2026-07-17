using EngineeringManager.Domain.Certificates;

namespace EngineeringManager.Application.Certificates;

public sealed record CertificateAttachmentUpload(string OriginalFileName, string ContentType, byte[] Content);

public sealed record CertificateFileDto(string OriginalFileName, string ContentType, byte[] Content);

public sealed record CertificateFilter(
    string? Search = null,
    Guid? OwnerId = null,
    string? CertificateType = null,
    CertificateExpiryState? State = null);

public sealed record EmployeeCertificateDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string CertificateType,
    string? CertificateNumber,
    string? SpecialtyLevelScope,
    string? IssuingAuthority,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    Guid? AttachmentId,
    string? AttachmentFileName,
    string? Notes,
    CertificateExpiryState State,
    Guid ConcurrencyStamp);

public sealed record SaveEmployeeCertificateRequest(
    Guid? Id,
    Guid EmployeeId,
    string CertificateType,
    string? CertificateNumber,
    string? SpecialtyLevelScope,
    string? IssuingAuthority,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    CertificateAttachmentUpload? NewAttachment,
    bool RemoveAttachment,
    string? Notes,
    Guid? ConcurrencyStamp,
    string Reason);

public sealed record CompanyCertificateItemDto(
    Guid Id,
    Guid LegalEntityId,
    string CompanyCode,
    string CompanyName,
    string CertificateType,
    string? CertificateNumber,
    string? SpecialtyLevelScope,
    string? IssuingAuthority,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    Guid? AttachmentId,
    string? AttachmentFileName,
    string? Notes,
    CertificateExpiryState State,
    Guid ConcurrencyStamp);

public sealed record SaveCompanyCertificateItemRequest(
    Guid? Id,
    Guid LegalEntityId,
    string CertificateType,
    string? CertificateNumber,
    string? SpecialtyLevelScope,
    string? IssuingAuthority,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    CertificateAttachmentUpload? NewAttachment,
    bool RemoveAttachment,
    string? Notes,
    Guid? ConcurrencyStamp,
    string Reason);
