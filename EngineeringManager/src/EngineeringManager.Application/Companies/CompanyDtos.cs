namespace EngineeringManager.Application.Companies;

public sealed record CompanyActor(
    string UserId,
    bool CanManage,
    bool CanAccessAllCompanies,
    IReadOnlyCollection<Guid> AccessibleCompanyIds)
{
    public static CompanyActor Administrator(string userId) => new(userId, true, true, []);
}

public sealed record CompanyCategoryDto(Guid Id, string Code, string Name, int SortOrder, bool IsActive, Guid ConcurrencyStamp);

public sealed record CompanyListItemDto(
    Guid Id,
    string Code,
    string Name,
    string ShortName,
    string? CategoryName,
    string? LegalRepresentative,
    bool IsActive,
    string? Notes = null);

public sealed record CompanyAccountDto(
    Guid Id,
    string AccountName,
    string? AccountNumber,
    string? BankName,
    string AccountType,
    decimal OpeningBalance,
    bool IsDefaultCollection,
    bool IsDefaultPayment,
    bool IsDefaultInvoice,
    bool IsActive,
    string? Notes = null);

public sealed record CompanyCertificateDto(
    Guid Id,
    string CertificateType,
    string? CertificateNumber,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    Guid? AttachmentId,
    string? Notes);

public sealed record CompanyDetailsDto(
    Guid Id,
    string Code,
    string Name,
    string ShortName,
    Guid? CompanyCategoryId,
    string? CompanyCategoryName,
    string? LegalRepresentative,
    string? UnifiedSocialCreditCode,
    string? RegisteredAddress,
    string? BusinessAddress,
    string? Phone,
    string? InvoiceTitle,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyStamp,
    IReadOnlyList<CompanyAccountDto> Accounts,
    IReadOnlyList<CompanyCertificateDto> Certificates);

public sealed record SaveCompanyRequest(
    Guid? Id,
    string Code,
    string Name,
    string ShortName,
    Guid? CompanyCategoryId,
    string? LegalRepresentative,
    string? UnifiedSocialCreditCode,
    string? RegisteredAddress,
    string? BusinessAddress,
    string? Phone,
    string? InvoiceTitle,
    string? Notes,
    Guid? ConcurrencyStamp,
    string Reason);

public sealed record SaveCompanyCategoryRequest(
    Guid? Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid? ConcurrencyStamp,
    string Reason);

public sealed record SaveCompanyAccountRequest(
    Guid? Id,
    Guid LegalEntityId,
    string AccountName,
    string? AccountNumber,
    string? BankName,
    int AccountType,
    decimal OpeningBalance,
    bool IsDefaultCollection,
    bool IsDefaultPayment,
    bool IsDefaultInvoice,
    bool IsActive,
    Guid? ConcurrencyStamp,
    string Reason,
    string? Notes = null);

public sealed record SaveCompanyCertificateRequest(
    Guid? Id,
    Guid LegalEntityId,
    string CertificateType,
    string? CertificateNumber,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    Guid? AttachmentId,
    string? Notes,
    Guid? ConcurrencyStamp,
    string Reason);

public sealed record CompanyDashboardDto(
    int CompanyCount,
    decimal ContractAmount,
    decimal EstimatedAmount,
    decimal SettledAmount,
    decimal ReceivableAmount,
    decimal CollectedAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal OutputInvoiceAmount,
    decimal InputInvoiceAmount,
    decimal PayrollCost,
    decimal EquipmentCost,
    decimal AccountBalance,
    DateTimeOffset GeneratedAt);
