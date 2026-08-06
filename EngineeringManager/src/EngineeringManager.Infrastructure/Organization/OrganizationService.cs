using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Organization;

public sealed class OrganizationService(ApplicationDbContext db) : IOrganizationService
{
    public async Task<OrganizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var organizationUnits = await db.OrganizationUnits
            .AsNoTracking()
            .OrderBy(unit => unit.Code)
            .Select(unit => new OrganizationUnitDto(unit.Id, unit.Code, unit.Name, unit.UnitType, unit.IsActive))
            .ToListAsync(cancellationToken);
        var legalEntities = await db.LegalEntities
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(entity => new LegalEntityDto(entity.Id, entity.Code, entity.Name, entity.ShortName, entity.IsActive))
            .ToListAsync(cancellationToken);
        return new OrganizationOverviewDto(organizationUnits, legalEntities);
    }

    public async Task<OrganizationUnitDto> CreateOrganizationUnitAsync(
        CreateOrganizationUnitRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (await db.OrganizationUnits.AnyAsync(unit => unit.Code == code && unit.LegalEntityId == null && unit.BusinessPartnerId == null, cancellationToken))
        {
            throw new InvalidOperationException($"组织编码已存在：{code}");
        }

        if (request.ParentId is not null && !await db.OrganizationUnits.AnyAsync(
                unit => unit.Id == request.ParentId && unit.IsActive && unit.LegalEntityId == null && unit.BusinessPartnerId == null,
                cancellationToken))
        {
            throw new InvalidOperationException("上级组织不存在或已停用。");
        }

        var entity = new OrganizationUnit
        {
            Code = code,
            Name = name,
            UnitType = request.UnitType,
            ParentId = request.ParentId
        };
        db.OrganizationUnits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new OrganizationUnitDto(entity.Id, entity.Code, entity.Name, entity.UnitType, entity.IsActive);
    }

    public async Task<LegalEntityDto> CreateLegalEntityAsync(
        CreateLegalEntityRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var shortName = NormalizeRequired(request.ShortName, nameof(request.ShortName));
        if (await db.LegalEntities.AnyAsync(entity => entity.Code == code, cancellationToken))
        {
            throw new InvalidOperationException($"签约公司编码已存在：{code}");
        }

        var entity = new LegalEntity
        {
            Code = code,
            Name = name,
            ShortName = shortName,
            CompanyCategoryId = CompanyCategoryDefaults.OtherId,
            UnifiedSocialCreditCode = string.IsNullOrWhiteSpace(request.UnifiedSocialCreditCode)
                ? null
                : request.UnifiedSocialCreditCode.Trim()
        };
        db.LegalEntities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new LegalEntityDto(entity.Id, entity.Code, entity.Name, entity.ShortName, entity.IsActive);
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(
        OrganizationOwnerKind ownerKind,
        Guid ownerId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var ownerName = await GetOwnerNameAsync(ownerKind, ownerId, requireActive: false, cancellationToken);
        IQueryable<OrganizationUnit> query = OwnedUnits(ownerKind, ownerId).AsNoTracking().Include(item => item.Parent);
        if (!includeInactive) query = query.Where(item => item.IsActive);
        var units = await query.OrderBy(item => item.Code).ThenBy(item => item.Name).ToArrayAsync(cancellationToken);
        var unitIds = units.Select(item => item.Id).ToArray();
        var counts = unitIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await db.PersonnelEngagementHistories.AsNoTracking()
                .Where(item => item.OrganizationUnitId.HasValue && unitIds.Contains(item.OrganizationUnitId.Value))
                .Where(item => item.Person.IsActive && item.IsPrimary)
                .Where(item => item.StartDate <= Today() && (item.EndDate == null || item.EndDate >= Today()))
                .GroupBy(item => item.OrganizationUnitId!.Value)
                .Select(group => new { DepartmentId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.DepartmentId, item => item.Count, cancellationToken);

        return units.Select(item => new DepartmentDto(
            item.Id,
            item.Code,
            item.Name,
            item.ParentId,
            item.Parent?.Name,
            ownerKind,
            ownerId,
            ownerName,
            item.IsAuthorizationScope,
            item.IsActive,
            counts.GetValueOrDefault(item.Id))).ToArray();
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OwnerId == Guid.Empty) throw new ArgumentException("所属组织不能为空。", nameof(request));
        await GetOwnerNameAsync(request.OwnerKind, request.OwnerId, requireActive: true, cancellationToken);
        var code = NormalizeRequired(request.Code, nameof(request.Code)).ToUpperInvariant();
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var ownerUnits = await OwnedUnits(request.OwnerKind, request.OwnerId).ToArrayAsync(cancellationToken);
        var entity = request.Id.HasValue
            ? ownerUnits.SingleOrDefault(item => item.Id == request.Id.Value)
                ?? throw new InvalidOperationException("部门不存在或不属于当前组织。")
            : new OrganizationUnit
            {
                UnitType = OrganizationUnitType.Department,
                LegalEntityId = request.OwnerKind == OrganizationOwnerKind.LegalEntity ? request.OwnerId : null,
                BusinessPartnerId = request.OwnerKind == OrganizationOwnerKind.BusinessPartner ? request.OwnerId : null
            };

        if (ownerUnits.Any(item => item.Id != entity.Id && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"当前组织内的部门编码已存在：{code}");
        }

        if (request.ParentId.HasValue)
        {
            var parent = ownerUnits.SingleOrDefault(item => item.Id == request.ParentId.Value)
                ?? throw new InvalidOperationException("上级部门必须属于同一组织。");
            if (!parent.IsActive) throw new InvalidOperationException("上级部门已停用。");
            EnsureNoParentCycle(entity.Id, parent, ownerUnits);
        }

        if (!request.IsActive && entity.IsActive && request.Id.HasValue)
        {
            await EnsureDepartmentCanBeDeactivatedAsync(entity.Id, cancellationToken);
        }

        entity.Code = code;
        entity.Name = name;
        entity.ParentId = request.ParentId;
        entity.UnitType = OrganizationUnitType.Department;
        entity.IsAuthorizationScope = request.IsAuthorizationScope;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (!request.Id.HasValue) db.OrganizationUnits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return (await ListDepartmentsAsync(request.OwnerKind, request.OwnerId, true, cancellationToken))
            .Single(item => item.Id == entity.Id);
    }

    public async Task DeactivateDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var entity = await db.OrganizationUnits.SingleOrDefaultAsync(item => item.Id == departmentId, cancellationToken)
            ?? throw new InvalidOperationException("部门不存在。");
        if (!entity.IsActive) return;
        await EnsureDepartmentCanBeDeactivatedAsync(entity.Id, cancellationToken);
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<OrganizationUnit> OwnedUnits(OrganizationOwnerKind ownerKind, Guid ownerId) => ownerKind switch
    {
        OrganizationOwnerKind.LegalEntity => db.OrganizationUnits.Where(item => item.LegalEntityId == ownerId && item.BusinessPartnerId == null),
        OrganizationOwnerKind.BusinessPartner => db.OrganizationUnits.Where(item => item.BusinessPartnerId == ownerId && item.LegalEntityId == null),
        _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "未知的组织所有者类型。")
    };

    private async Task<string> GetOwnerNameAsync(
        OrganizationOwnerKind ownerKind,
        Guid ownerId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        return ownerKind switch
        {
            OrganizationOwnerKind.LegalEntity => await db.LegalEntities.AsNoTracking()
                .Where(item => item.Id == ownerId && (!requireActive || item.IsActive))
                .Select(item => item.ShortName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("自有公司不存在或已停用。"),
            OrganizationOwnerKind.BusinessPartner => await db.BusinessPartners.AsNoTracking()
                .Where(item => item.Id == ownerId && (!requireActive || item.IsActive))
                .Select(item => item.ShortName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("合作单位不存在或已停用。"),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "未知的组织所有者类型。")
        };
    }

    private async Task EnsureDepartmentCanBeDeactivatedAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var today = Today();
        var isReferenced = await db.PersonnelEngagementHistories.AsNoTracking().AnyAsync(
            item => item.OrganizationUnitId == departmentId
                    && item.Person.IsActive
                    && item.IsPrimary
                    && item.StartDate <= today
                    && (item.EndDate == null || item.EndDate >= today),
            cancellationToken);
        if (isReferenced) throw new InvalidOperationException("该部门仍有当前人员归属，不能停用。");
    }

    private static void EnsureNoParentCycle(Guid departmentId, OrganizationUnit parent, IReadOnlyList<OrganizationUnit> ownerUnits)
    {
        if (departmentId == Guid.Empty) return;
        var byId = ownerUnits.ToDictionary(item => item.Id);
        var current = parent;
        while (true)
        {
            if (current.Id == departmentId) throw new InvalidOperationException("部门层级不能形成循环。");
            if (!current.ParentId.HasValue || !byId.TryGetValue(current.ParentId.Value, out var next)) return;
            current = next;
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }
}
