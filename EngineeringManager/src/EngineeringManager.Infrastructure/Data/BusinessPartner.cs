namespace EngineeringManager.Infrastructure.Data;

public sealed class BusinessPartner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PartnerNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<BusinessPartnerRole> Roles { get; set; } = [];
    public ICollection<PartnerContact> Contacts { get; set; } = [];
    public ICollection<ProjectPartner> ProjectLinks { get; set; } = [];
}
