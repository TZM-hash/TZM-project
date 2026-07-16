namespace EngineeringManager.Domain.Organization;

public sealed class LegalEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public string? UnifiedSocialCreditCode { get; set; }

    public Guid? CompanyCategoryId { get; set; }

    public CompanyCategory? CompanyCategory { get; set; }

    public string? LegalRepresentative { get; set; }

    public string? RegisteredAddress { get; set; }

    public string? BusinessAddress { get; set; }

    public string? Phone { get; set; }

    public string? InvoiceTitle { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
