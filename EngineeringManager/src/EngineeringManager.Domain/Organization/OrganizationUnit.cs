namespace EngineeringManager.Domain.Organization;

public sealed class OrganizationUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public OrganizationUnitType UnitType { get; set; }

    public Guid? ParentId { get; set; }

    public OrganizationUnit? Parent { get; set; }

    public ICollection<OrganizationUnit> Children { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
