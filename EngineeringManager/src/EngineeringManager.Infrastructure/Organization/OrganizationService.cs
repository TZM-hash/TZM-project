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
        if (await db.OrganizationUnits.AnyAsync(unit => unit.Code == code, cancellationToken))
        {
            throw new InvalidOperationException($"组织编码已存在：{code}");
        }

        if (request.ParentId is not null && !await db.OrganizationUnits.AnyAsync(unit => unit.Id == request.ParentId && unit.IsActive, cancellationToken))
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

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }
}
