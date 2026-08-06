using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Organization;

public sealed record OrganizationSummaryQuery(OrganizationOwnerKind Kind, Guid Id, DateOnly AsOf);

public sealed record OrganizationProjectStatsDto(
    int TotalCount,
    int AwaitingMobilizationCount,
    int UnderConstructionCount,
    int SuspendedCount,
    int CompletedUnsettledCount,
    int PartiallySettledCount,
    int SettledArchivedCount)
{
    public int InProgressCount => AwaitingMobilizationCount + UnderConstructionCount;
}

public sealed record OrganizationPersonnelStatsDto(
    int TotalCurrentCount,
    int ActiveCount,
    int FormalCount,
    int LaborCount,
    int TemporaryCount,
    int ConstructionCrewCount,
    int BusinessPartnerCount,
    int OtherExternalCount);

public sealed record OrganizationDepartmentStatsDto(int TotalCount, int ActiveCount);

public sealed record OrganizationSummaryDto(
    OrganizationSummaryQuery Query,
    OrganizationProjectStatsDto Projects,
    OrganizationPersonnelStatsDto Personnel,
    OrganizationDepartmentStatsDto Departments,
    bool IsConstructionCrew);

public static class OrganizationSummaryLinks
{
    public static string Projects(OrganizationOwnerKind ownerKind, Guid ownerId, params ProjectStage[] stages)
    {
        var ownerFilter = ownerKind == OrganizationOwnerKind.LegalEntity
            ? $"LegalEntityId={ownerId}"
            : $"BusinessPartnerId={ownerId}";
        var stageFilter = stages.Length == 0
            ? string.Empty
            : string.Concat(stages.Distinct().Select(stage => $"&Stages={(int)stage}"));
        return $"/Projects?{ownerFilter}{stageFilter}";
    }

    public static string InternalPersonnel(Guid legalEntityId, EmployeeType? internalType = null, bool? isActive = true) =>
        $"/Personnel/Internal?LegalEntityId={legalEntityId}{ActiveFilter(isActive)}{(internalType.HasValue ? $"&InternalType={internalType.Value}" : string.Empty)}";

    public static string ExternalPersonnel(Guid businessPartnerId, ExternalPersonnelType? externalType = null, bool? isActive = true) =>
        $"/Personnel/External?BusinessPartnerId={businessPartnerId}{ActiveFilter(isActive)}{(externalType.HasValue ? $"&ExternalType={externalType.Value}" : string.Empty)}";

    public static string Departments(OrganizationOwnerKind ownerKind, Guid ownerId) => ownerKind == OrganizationOwnerKind.LegalEntity
        ? $"/Organization/Departments?LegalEntityId={ownerId}"
        : $"/Organization/Departments?BusinessPartnerId={ownerId}";

    private static string ActiveFilter(bool? isActive) => isActive switch
    {
        true => "&IsActive=true",
        false => "&IsActive=false",
        null => string.Empty
    };
}
