using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Organization;

public sealed class OrganizationSummaryService(ApplicationDbContext db) : IOrganizationSummaryService
{
    public async Task<OrganizationSummaryDto> GetAsync(OrganizationSummaryQuery query, CancellationToken cancellationToken)
    {
        if (query.Id == Guid.Empty) throw new ArgumentException("组织标识不能为空。", nameof(query));
        var isConstructionCrew = await ValidateOwnerAsync(query, cancellationToken);
        var projectStages = await LoadProjectStagesAsync(query, isConstructionCrew, cancellationToken);
        var personnelRows = await LoadPersonnelRowsAsync(query, isConstructionCrew, cancellationToken);
        var departments = await OwnedDepartments(query).AsNoTracking().Select(item => item.IsActive).ToArrayAsync(cancellationToken);

        return new OrganizationSummaryDto(
            query,
            new OrganizationProjectStatsDto(
                projectStages.Length,
                projectStages.Count(stage => stage == ProjectStage.AwaitingMobilization),
                projectStages.Count(stage => stage == ProjectStage.UnderConstruction),
                projectStages.Count(stage => stage == ProjectStage.Suspended),
                projectStages.Count(stage => stage == ProjectStage.CompletedUnsettled),
                projectStages.Count(stage => stage == ProjectStage.PartiallySettled),
                projectStages.Count(stage => stage == ProjectStage.SettledArchived)),
            new OrganizationPersonnelStatsDto(
                personnelRows.Count,
                personnelRows.Count(item => item.IsActive),
                personnelRows.Count(item => item.IsActive && item.InternalType == EmployeeType.Formal),
                personnelRows.Count(item => item.IsActive && item.InternalType == EmployeeType.Labor),
                personnelRows.Count(item => item.IsActive && item.InternalType == EmployeeType.Temporary),
                personnelRows.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.ConstructionCrew),
                personnelRows.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.BusinessPartner),
                personnelRows.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.Other)),
            new OrganizationDepartmentStatsDto(departments.Length, departments.Count(isActive => isActive)),
            isConstructionCrew);
    }

    public async Task<IReadOnlyList<OrganizationSummaryDto>> GetManyAsync(
        IReadOnlyCollection<OrganizationSummaryQuery> queries,
        CancellationToken cancellationToken)
    {
        if (queries.Count == 0)
        {
            return [];
        }

        var requested = queries.Distinct().ToArray();
        foreach (var query in requested)
        {
            if (query.Id == Guid.Empty) throw new ArgumentException("组织标识不能为空。", nameof(queries));
            if (query.Kind is not OrganizationOwnerKind.LegalEntity and not OrganizationOwnerKind.BusinessPartner)
            {
                throw new ArgumentOutOfRangeException(nameof(queries), query.Kind, "未知的组织所有者类型。");
            }
        }

        var summaries = new Dictionary<OrganizationSummaryQuery, OrganizationSummaryDto>();
        foreach (var group in requested.GroupBy(item => (item.Kind, item.AsOf)))
        {
            var groupSummaries = group.Key.Kind == OrganizationOwnerKind.LegalEntity
                ? await LoadLegalEntitySummariesAsync(group.Select(item => item.Id).ToArray(), group.Key.AsOf, cancellationToken)
                : await LoadBusinessPartnerSummariesAsync(group.Select(item => item.Id).ToArray(), group.Key.AsOf, cancellationToken);
            foreach (var query in group)
            {
                summaries[query] = groupSummaries[query.Id];
            }
        }

        return requested.Select(item => summaries[item]).ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, OrganizationSummaryDto>> LoadLegalEntitySummariesAsync(
        Guid[] ownerIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var existingIds = await db.LegalEntities.AsNoTracking()
            .Where(item => ownerIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        EnsureAllOwnersExist(ownerIds, existingIds, "自有公司不存在。");

        var projectRows = await db.ProjectLegalEntities.AsNoTracking()
            .Where(item => ownerIds.Contains(item.LegalEntityId) && item.Project.IsActive)
            .Select(item => new OwnerProjectRow(item.LegalEntityId, item.ProjectId, item.Project.Stage))
            .ToArrayAsync(cancellationToken);
        var personnelRows = await db.PersonnelEngagementHistories.AsNoTracking()
            .Where(item => item.IsPrimary
                && item.StartDate <= asOf
                && (item.EndDate == null || item.EndDate >= asOf)
                && item.LegalEntityId.HasValue
                && ownerIds.Contains(item.LegalEntityId.Value))
            .Select(item => new OwnerPersonnelRow(
                item.LegalEntityId!.Value,
                item.PersonId,
                item.Person.IsActive,
                item.InternalType,
                item.ExternalType,
                item.StartDate))
            .ToArrayAsync(cancellationToken);
        var departmentRows = await db.OrganizationUnits.AsNoTracking()
            .Where(item => item.LegalEntityId.HasValue
                && item.BusinessPartnerId == null
                && ownerIds.Contains(item.LegalEntityId.Value))
            .Select(item => new OwnerDepartmentRow(item.LegalEntityId!.Value, item.IsActive))
            .ToArrayAsync(cancellationToken);

        return BuildSummaries(
            OrganizationOwnerKind.LegalEntity,
            ownerIds,
            asOf,
            ownerIds.ToDictionary(item => item, _ => false),
            projectRows,
            personnelRows,
            departmentRows,
            []);
    }

    private async Task<IReadOnlyDictionary<Guid, OrganizationSummaryDto>> LoadBusinessPartnerSummariesAsync(
        Guid[] ownerIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var owners = await db.BusinessPartners.AsNoTracking()
            .Where(item => ownerIds.Contains(item.Id))
            .Select(item => new PartnerOwnerRow(
                item.Id,
                item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew)))
            .ToArrayAsync(cancellationToken);
        EnsureAllOwnersExist(ownerIds, owners.Select(item => item.Id), "合作单位不存在。");
        var isConstructionCrew = owners.ToDictionary(item => item.Id, item => item.IsConstructionCrew);
        var crewIds = owners.Where(item => item.IsConstructionCrew).Select(item => item.Id).ToArray();

        var projectRows = (await db.ProjectPartners.AsNoTracking()
            .Where(item => ownerIds.Contains(item.BusinessPartnerId) && item.IsActive && item.Project.IsActive)
            .Select(item => new OwnerProjectRow(item.BusinessPartnerId, item.ProjectId, item.Project.Stage))
            .ToListAsync(cancellationToken));
        if (crewIds.Length > 0)
        {
            projectRows.AddRange(await db.ProjectConstructionRecords.AsNoTracking()
                .Where(item => item.CrewBusinessPartnerId.HasValue
                    && crewIds.Contains(item.CrewBusinessPartnerId.Value)
                    && item.Project.IsActive)
                .Select(item => new OwnerProjectRow(item.CrewBusinessPartnerId!.Value, item.ProjectId, item.Project.Stage))
                .ToListAsync(cancellationToken));
        }

        var engagementSources = await db.PersonnelEngagementHistories.AsNoTracking()
            .Where(item => item.IsPrimary
                && item.StartDate <= asOf
                && (item.EndDate == null || item.EndDate >= asOf)
                && item.BusinessPartnerId.HasValue
                && ownerIds.Contains(item.BusinessPartnerId.Value))
            .Select(item => new PartnerPersonnelSource(
                item.BusinessPartnerId,
                item.CrewBusinessPartnerId,
                item.PersonId,
                item.Person.IsActive,
                item.InternalType,
                item.ExternalType,
                item.StartDate))
            .ToArrayAsync(cancellationToken);
        var ownerIdSet = ownerIds.ToHashSet();
        var personnelRows = new List<OwnerPersonnelRow>(engagementSources.Length);
        foreach (var source in engagementSources)
        {
            if (source.BusinessPartnerId is Guid ownerId && ownerIdSet.Contains(ownerId))
            {
                personnelRows.Add(new OwnerPersonnelRow(
                    ownerId,
                    source.PersonId,
                    source.IsActive,
                    source.InternalType,
                    source.ExternalType,
                    source.StartDate));
            }
        }

        var departmentRows = await db.OrganizationUnits.AsNoTracking()
            .Where(item => item.BusinessPartnerId.HasValue
                && item.LegalEntityId == null
                && ownerIds.Contains(item.BusinessPartnerId.Value))
            .Select(item => new OwnerDepartmentRow(item.BusinessPartnerId!.Value, item.IsActive))
            .ToArrayAsync(cancellationToken);

        return BuildSummaries(
            OrganizationOwnerKind.BusinessPartner,
            ownerIds,
            asOf,
            isConstructionCrew,
            projectRows,
            personnelRows,
            departmentRows,
            []);
    }

    private static Dictionary<Guid, OrganizationSummaryDto> BuildSummaries(
        OrganizationOwnerKind kind,
        IEnumerable<Guid> ownerIds,
        DateOnly asOf,
        IReadOnlyDictionary<Guid, bool> isConstructionCrew,
        IEnumerable<OwnerProjectRow> projectRows,
        IEnumerable<OwnerPersonnelRow> personnelRows,
        IEnumerable<OwnerDepartmentRow> departmentRows,
        IEnumerable<OwnerCrewMembershipRow> membershipRows)
    {
        var projectsByOwner = projectRows
            .GroupBy(item => item.OwnerId)
            .ToDictionary(
                group => group.Key,
                group => group.GroupBy(item => item.ProjectId).Select(project => project.First().Stage).ToArray());
        var personnelByOwner = personnelRows.GroupBy(item => item.OwnerId).ToDictionary(group => group.Key, group => group.ToArray());
        var departmentsByOwner = departmentRows.GroupBy(item => item.OwnerId).ToDictionary(group => group.Key, group => group.Select(item => item.IsActive).ToArray());
        var membershipsByOwner = membershipRows.GroupBy(item => item.OwnerId).ToDictionary(group => group.Key, group => group.ToArray());
        var summaries = new Dictionary<Guid, OrganizationSummaryDto>();

        foreach (var ownerId in ownerIds)
        {
            var stages = projectsByOwner.GetValueOrDefault(ownerId) ?? [];
            var currentPersonnel = (personnelByOwner.GetValueOrDefault(ownerId) ?? [])
                .GroupBy(item => item.PersonId)
                .Select(group => group.OrderByDescending(item => item.StartDate).First())
                .ToDictionary(item => item.PersonId);
            if (isConstructionCrew.GetValueOrDefault(ownerId)
                && membershipsByOwner.TryGetValue(ownerId, out var memberships))
            {
                foreach (var membership in memberships.OrderByDescending(item => item.StartDate))
                {
                    currentPersonnel.TryAdd(
                        membership.PersonId,
                        new OwnerPersonnelRow(
                            ownerId,
                            membership.PersonId,
                            membership.IsActive,
                            null,
                            ExternalPersonnelType.ConstructionCrew,
                            membership.StartDate));
                }
            }
            var personnel = currentPersonnel.Values.ToArray();
            var departments = departmentsByOwner.GetValueOrDefault(ownerId) ?? [];
            var query = new OrganizationSummaryQuery(kind, ownerId, asOf);
            summaries[ownerId] = new OrganizationSummaryDto(
                query,
                new OrganizationProjectStatsDto(
                    stages.Length,
                    stages.Count(stage => stage == ProjectStage.AwaitingMobilization),
                    stages.Count(stage => stage == ProjectStage.UnderConstruction),
                    stages.Count(stage => stage == ProjectStage.Suspended),
                    stages.Count(stage => stage == ProjectStage.CompletedUnsettled),
                    stages.Count(stage => stage == ProjectStage.PartiallySettled),
                    stages.Count(stage => stage == ProjectStage.SettledArchived)),
                new OrganizationPersonnelStatsDto(
                    personnel.Length,
                    personnel.Count(item => item.IsActive),
                    personnel.Count(item => item.IsActive && item.InternalType == EmployeeType.Formal),
                    personnel.Count(item => item.IsActive && item.InternalType == EmployeeType.Labor),
                    personnel.Count(item => item.IsActive && item.InternalType == EmployeeType.Temporary),
                    personnel.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.ConstructionCrew),
                    personnel.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.BusinessPartner),
                    personnel.Count(item => item.IsActive && item.ExternalType == ExternalPersonnelType.Other)),
                new OrganizationDepartmentStatsDto(departments.Length, departments.Count(item => item)),
                isConstructionCrew.GetValueOrDefault(ownerId));
        }

        return summaries;
    }

    private static void EnsureAllOwnersExist(IEnumerable<Guid> requestedIds, IEnumerable<Guid> existingIds, string message)
    {
        var existing = existingIds.ToHashSet();
        if (requestedIds.Any(item => !existing.Contains(item)))
        {
            throw new InvalidOperationException(message);
        }
    }

    private async Task<bool> ValidateOwnerAsync(OrganizationSummaryQuery query, CancellationToken cancellationToken)
    {
        if (query.Kind == OrganizationOwnerKind.LegalEntity)
        {
            if (!await db.LegalEntities.AsNoTracking().AnyAsync(item => item.Id == query.Id, cancellationToken))
            {
                throw new InvalidOperationException("自有公司不存在。");
            }
            return false;
        }
        if (query.Kind != OrganizationOwnerKind.BusinessPartner)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Kind, "未知的组织所有者类型。");
        }
        if (!await db.BusinessPartners.AsNoTracking().AnyAsync(item => item.Id == query.Id, cancellationToken))
        {
            throw new InvalidOperationException("合作单位不存在。");
        }
        return await db.BusinessPartnerRoles.AsNoTracking().AnyAsync(
            item => item.BusinessPartnerId == query.Id && item.RoleType == BusinessPartnerRoleType.ConstructionCrew,
            cancellationToken);
    }

    private async Task<ProjectStage[]> LoadProjectStagesAsync(
        OrganizationSummaryQuery query,
        bool isConstructionCrew,
        CancellationToken cancellationToken)
    {
        IQueryable<Guid> projectIds;
        if (query.Kind == OrganizationOwnerKind.LegalEntity)
        {
            projectIds = db.ProjectLegalEntities.AsNoTracking()
                .Where(item => item.LegalEntityId == query.Id)
                .Select(item => item.ProjectId);
        }
        else
        {
            var partnerIds = db.ProjectPartners.AsNoTracking()
                .Where(item => item.BusinessPartnerId == query.Id && item.IsActive)
                .Select(item => item.ProjectId);
            projectIds = isConstructionCrew
                ? partnerIds.Union(db.ProjectConstructionRecords.AsNoTracking()
                    .Where(item => item.CrewBusinessPartnerId == query.Id)
                    .Select(item => item.ProjectId))
                : partnerIds;
        }

        return await db.Projects.AsNoTracking()
            .Where(item => item.IsActive && projectIds.Contains(item.Id))
            .Select(item => item.Stage)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<List<PersonnelRow>> LoadPersonnelRowsAsync(
        OrganizationSummaryQuery query,
        bool isConstructionCrew,
        CancellationToken cancellationToken)
    {
        var engagementQuery = db.PersonnelEngagementHistories.AsNoTracking()
            .Where(item => item.IsPrimary && item.StartDate <= query.AsOf && (item.EndDate == null || item.EndDate >= query.AsOf));
        engagementQuery = query.Kind == OrganizationOwnerKind.LegalEntity
            ? engagementQuery.Where(item => item.LegalEntityId == query.Id)
            : engagementQuery.Where(item => item.BusinessPartnerId == query.Id);
        var engagements = await engagementQuery
            .Select(item => new PersonnelRow(item.PersonId, item.Person.IsActive, item.InternalType, item.ExternalType, item.StartDate))
            .ToListAsync(cancellationToken);
        var current = engagements.GroupBy(item => item.PersonId)
            .Select(group => group.OrderByDescending(item => item.StartDate).First())
            .ToDictionary(item => item.PersonId);

        return current.Values.ToList();
    }

    private IQueryable<OrganizationUnit> OwnedDepartments(OrganizationSummaryQuery query) => query.Kind switch
    {
        OrganizationOwnerKind.LegalEntity => db.OrganizationUnits.Where(item => item.LegalEntityId == query.Id && item.BusinessPartnerId == null),
        OrganizationOwnerKind.BusinessPartner => db.OrganizationUnits.Where(item => item.BusinessPartnerId == query.Id && item.LegalEntityId == null),
        _ => throw new ArgumentOutOfRangeException(nameof(query), query.Kind, "未知的组织所有者类型。")
    };

    private sealed record PersonnelRow(
        Guid PersonId,
        bool IsActive,
        EmployeeType? InternalType,
        ExternalPersonnelType? ExternalType,
        DateOnly StartDate);

    private sealed record PartnerOwnerRow(Guid Id, bool IsConstructionCrew);
    private sealed record OwnerProjectRow(Guid OwnerId, Guid ProjectId, ProjectStage Stage);
    private sealed record OwnerPersonnelRow(
        Guid OwnerId,
        Guid PersonId,
        bool IsActive,
        EmployeeType? InternalType,
        ExternalPersonnelType? ExternalType,
        DateOnly StartDate);
    private sealed record PartnerPersonnelSource(
        Guid? BusinessPartnerId,
        Guid? CrewBusinessPartnerId,
        Guid PersonId,
        bool IsActive,
        EmployeeType? InternalType,
        ExternalPersonnelType? ExternalType,
        DateOnly StartDate);
    private sealed record OwnerDepartmentRow(Guid OwnerId, bool IsActive);
    private sealed record OwnerCrewMembershipRow(Guid OwnerId, Guid PersonId, bool IsActive, DateOnly StartDate);
}
