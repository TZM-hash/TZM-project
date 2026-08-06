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
            : engagementQuery.Where(item => item.BusinessPartnerId == query.Id || item.CrewBusinessPartnerId == query.Id);
        var engagements = await engagementQuery
            .Select(item => new PersonnelRow(item.PersonId, item.Person.IsActive, item.InternalType, item.ExternalType, item.StartDate))
            .ToListAsync(cancellationToken);
        var current = engagements.GroupBy(item => item.PersonId)
            .Select(group => group.OrderByDescending(item => item.StartDate).First())
            .ToDictionary(item => item.PersonId);

        if (query.Kind == OrganizationOwnerKind.BusinessPartner && isConstructionCrew)
        {
            var memberships = await db.ConstructionCrewMemberships.AsNoTracking()
                .Where(item => item.CrewBusinessPartnerId == query.Id && item.StartDate <= query.AsOf && (item.EndDate == null || item.EndDate >= query.AsOf))
                .Select(item => new
                {
                    Key = item.Worker.PersonId ?? item.ConstructionWorkerId,
                    IsActive = item.Worker.Person != null ? item.Worker.Person.IsActive : item.Worker.IsActive,
                    item.StartDate
                })
                .ToArrayAsync(cancellationToken);
            foreach (var membership in memberships)
            {
                if (!current.ContainsKey(membership.Key))
                {
                    current[membership.Key] = new PersonnelRow(
                        membership.Key,
                        membership.IsActive,
                        null,
                        ExternalPersonnelType.ConstructionCrew,
                        membership.StartDate);
                }
            }
        }

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
}
