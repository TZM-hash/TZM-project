using EngineeringManager.Domain.Partners;

namespace EngineeringManager.Application.Partners;

public sealed record PartnerRoleRequest(
    BusinessPartnerRoleType RoleType,
    string? TradeCategory,
    string? PricingRule,
    string? SettlementTerms);

public sealed record PartnerContactRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsPrimary);

public sealed record CreateBusinessPartnerRequest(
    string PartnerNumber,
    string Name,
    string ShortName,
    string? UnifiedSocialCreditCode,
    string? Notes,
    IReadOnlyCollection<PartnerRoleRequest> Roles,
    IReadOnlyCollection<PartnerContactRequest> Contacts);

public sealed record CopyBusinessPartnerRequest(
    Guid SourcePartnerId,
    string PartnerNumber,
    string Name,
    string ShortName);

public sealed record LinkPartnerToProjectRequest(
    Guid PartnerId,
    Guid ProjectId,
    BusinessPartnerRoleType RoleType,
    Guid? ContractId,
    bool IsPrimary);

public sealed record PartnerRoleDto(
    BusinessPartnerRoleType RoleType,
    string? TradeCategory,
    string? PricingRule,
    string? SettlementTerms);

public sealed record PartnerContactDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsPrimary);

public sealed record BusinessPartnerDto(
    Guid Id,
    string PartnerNumber,
    string Name,
    string ShortName,
    string? UnifiedSocialCreditCode,
    string? Notes,
    IReadOnlyList<PartnerRoleDto> Roles,
    IReadOnlyList<PartnerContactDto> Contacts,
    int ProjectCount);
