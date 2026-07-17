using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserOrganizationMembership> OrganizationMemberships { get; set; } = [];

    public ICollection<UserLegalEntityAccess> LegalEntityAccesses { get; set; } = [];

    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = [];

    public ICollection<UserDataScope> DataScopes { get; set; } = [];

    public ICollection<SavedDataView> SavedDataViews { get; set; } = [];
}
