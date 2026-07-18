namespace EngineeringManager.Infrastructure.Data;

public sealed class PartnerContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner Partner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
}
