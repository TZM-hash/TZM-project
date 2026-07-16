using EngineeringManager.Domain.Partners;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ProjectPartner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner Partner { get; set; } = null!;
    public BusinessPartnerRoleType RoleType { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}
