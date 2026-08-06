using EngineeringManager.Application.Organization;

namespace EngineeringManager.Web;

public sealed record OrganizationSummaryViewModel(
    OrganizationSummaryDto Summary,
    bool Compact = false);
