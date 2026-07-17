namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeCertificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string CertificateType { get; set; } = string.Empty;
    public string? CertificateNumber { get; set; }
    public string? SpecialtyLevelScope { get; set; }
    public string? IssuingAuthority { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public Guid? AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
