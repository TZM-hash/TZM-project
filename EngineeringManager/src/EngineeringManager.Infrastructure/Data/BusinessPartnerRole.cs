using EngineeringManager.Domain.Partners;

namespace EngineeringManager.Infrastructure.Data;

public sealed class BusinessPartnerRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner Partner { get; set; } = null!;
    public BusinessPartnerRoleType RoleType { get; set; }
    public string? TradeCategory { get; set; }
    public string? PricingRule { get; set; }
    public string? SettlementTerms { get; set; }
}
