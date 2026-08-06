using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Partners;

internal sealed class BusinessPartnerRoleChangeCoordinator(ApplicationDbContext db)
{
    public async Task ReplaceAsync(
        BusinessPartner partner,
        BusinessPartnerRole previousRole,
        BusinessPartnerRoleType targetRoleType,
        CancellationToken cancellationToken)
    {
        if (previousRole.RoleType == targetRoleType)
        {
            return;
        }

        await ValidateRemovalAsync(partner, previousRole.RoleType, targetRoleType, cancellationToken);
        ValidateProjectLinkRoleChange(partner, previousRole.RoleType, targetRoleType);
        ChangeProjectLinkRole(partner, previousRole.RoleType, targetRoleType);

        var targetRole = partner.Roles.FirstOrDefault(item => item.RoleType == targetRoleType);
        if (targetRole is null)
        {
            previousRole.RoleType = targetRoleType;
            return;
        }

        partner.Roles.Remove(previousRole);
        db.BusinessPartnerRoles.Remove(previousRole);
    }

    public async Task ApplyImportedRoleSetAsync(
        BusinessPartner partner,
        IReadOnlySet<BusinessPartnerRoleType> requestedRoles,
        CancellationToken cancellationToken)
    {
        var removedRoles = partner.Roles.Where(item => !requestedRoles.Contains(item.RoleType)).ToArray();
        var addedRoleTypes = requestedRoles.Where(roleType => partner.Roles.All(item => item.RoleType != roleType)).ToArray();
        if (removedRoles.Length == 1 && addedRoleTypes.Length == 1)
        {
            await ReplaceAsync(partner, removedRoles[0], addedRoleTypes[0], cancellationToken);
        }
        else
        {
            foreach (var removedRole in removedRoles)
            {
                await ValidateRemovalAsync(partner, removedRole.RoleType, null, cancellationToken);
                if (partner.ProjectLinks.Any(item => item.RoleType == removedRole.RoleType))
                {
                    throw new InvalidOperationException("导入同时变更多个业务角色时，无法确定项目关联应迁移到哪个新角色，请改为一次只替换一个角色。");
                }

                partner.Roles.Remove(removedRole);
                db.BusinessPartnerRoles.Remove(removedRole);
            }
        }

        foreach (var roleType in requestedRoles.Where(roleType => partner.Roles.All(item => item.RoleType != roleType)))
        {
            var role = new BusinessPartnerRole { Partner = partner, RoleType = roleType };
            partner.Roles.Add(role);
            db.BusinessPartnerRoles.Add(role);
        }
    }

    private async Task ValidateRemovalAsync(
        BusinessPartner partner,
        BusinessPartnerRoleType previousRoleType,
        BusinessPartnerRoleType? targetRoleType,
        CancellationToken cancellationToken)
    {
        if (previousRoleType != BusinessPartnerRoleType.ConstructionCrew
            || targetRoleType == BusinessPartnerRoleType.ConstructionCrew)
        {
            return;
        }

        if (await db.ProjectConstructionRecords.AnyAsync(item => item.CrewBusinessPartnerId == partner.Id, cancellationToken))
        {
            throw new InvalidOperationException("该施工班组仍被项目施工记录引用，无法改为其他类型，请先调整项目施工记录。");
        }
        if (await db.ConstructionCrewMemberships.AnyAsync(item => item.CrewBusinessPartnerId == partner.Id, cancellationToken)
            || await db.PersonnelEngagementHistories.AnyAsync(
                item => item.CrewBusinessPartnerId == partner.Id
                    || item.ExternalType == ExternalPersonnelType.ConstructionCrew && item.BusinessPartnerId == partner.Id,
                cancellationToken))
        {
            throw new InvalidOperationException("该施工班组已有人员或历史归属，无法改为其他类型，请先调整人员归属。");
        }
    }

    private void ChangeProjectLinkRole(
        BusinessPartner partner,
        BusinessPartnerRoleType previousRoleType,
        BusinessPartnerRoleType roleType)
    {
        foreach (var projectLink in partner.ProjectLinks.Where(item => item.RoleType == previousRoleType).ToArray())
        {
            var existingTarget = partner.ProjectLinks.FirstOrDefault(item =>
                item.Id != projectLink.Id
                && item.ProjectId == projectLink.ProjectId
                && item.RoleType == roleType);
            if (existingTarget is null)
            {
                projectLink.RoleType = roleType;
                continue;
            }

            existingTarget.ContractId ??= projectLink.ContractId;
            existingTarget.IsPrimary |= projectLink.IsPrimary;
            existingTarget.IsActive |= projectLink.IsActive;
            if (string.IsNullOrWhiteSpace(existingTarget.Notes))
            {
                existingTarget.Notes = projectLink.Notes;
            }
            partner.ProjectLinks.Remove(projectLink);
            db.ProjectPartners.Remove(projectLink);
        }
    }

    private static void ValidateProjectLinkRoleChange(
        BusinessPartner partner,
        BusinessPartnerRoleType previousRoleType,
        BusinessPartnerRoleType roleType)
    {
        foreach (var projectLink in partner.ProjectLinks.Where(item => item.RoleType == previousRoleType))
        {
            var existingTarget = partner.ProjectLinks.FirstOrDefault(item =>
                item.Id != projectLink.Id
                && item.ProjectId == projectLink.ProjectId
                && item.RoleType == roleType);
            if (existingTarget is null)
            {
                continue;
            }

            if (existingTarget.ContractId.HasValue
                && projectLink.ContractId.HasValue
                && existingTarget.ContractId != projectLink.ContractId)
            {
                throw new InvalidOperationException("同一项目的原角色与目标角色关联了不同合同，无法合并，请先统一合同。");
            }
            if (!string.IsNullOrWhiteSpace(existingTarget.Notes)
                && !string.IsNullOrWhiteSpace(projectLink.Notes)
                && !string.Equals(existingTarget.Notes, projectLink.Notes, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("同一项目的原角色与目标角色备注不同，无法合并，请先统一备注。");
            }
        }
    }
}
