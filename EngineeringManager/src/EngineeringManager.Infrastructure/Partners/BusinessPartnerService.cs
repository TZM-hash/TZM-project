using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Partners;

public sealed class BusinessPartnerService(ApplicationDbContext db) : IBusinessPartnerService
{
    public async Task<BusinessPartnerDto> CreateAsync(
        CreateBusinessPartnerRequest request,
        CancellationToken cancellationToken)
    {
        var partnerNumber = NormalizeRequired(request.PartnerNumber, nameof(request.PartnerNumber));
        if (await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == partnerNumber, cancellationToken))
        {
            throw new InvalidOperationException($"合作单位编号已存在：{partnerNumber}");
        }

        var creditCode = NormalizeOptional(request.UnifiedSocialCreditCode);
        if (creditCode is not null && await db.BusinessPartners.AnyAsync(item => item.UnifiedSocialCreditCode == creditCode, cancellationToken))
        {
            throw new InvalidOperationException("统一社会信用代码已存在。");
        }

        var partner = new BusinessPartner
        {
            PartnerNumber = partnerNumber,
            Name = NormalizeRequired(request.Name, nameof(request.Name)),
            ShortName = NormalizeRequired(request.ShortName, nameof(request.ShortName)),
            UnifiedSocialCreditCode = creditCode,
            Notes = NormalizeOptional(request.Notes)
        };
        AddRoles(partner, request.Roles);
        foreach (var contact in request.Contacts)
        {
            partner.Contacts.Add(new PartnerContact
            {
                Partner = partner,
                Name = NormalizeRequired(contact.Name, nameof(contact.Name)),
                Phone = NormalizeOptional(contact.Phone),
                Email = NormalizeOptional(contact.Email),
                Address = NormalizeOptional(contact.Address),
                IsPrimary = contact.IsPrimary
            });
        }

        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(partner);
    }

    public async Task<BusinessPartnerDto> CopyAsync(
        CopyBusinessPartnerRequest request,
        CancellationToken cancellationToken)
    {
        var source = await db.BusinessPartners
            .AsNoTracking()
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == request.SourcePartnerId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("原合作单位不存在或已停用。");
        return await CreateAsync(
            new CreateBusinessPartnerRequest(
                request.PartnerNumber,
                request.Name,
                request.ShortName,
                null,
                source.Notes,
                source.Roles.Select(role => new PartnerRoleRequest(role.RoleType, role.TradeCategory, role.PricingRule, role.SettlementTerms)).ToArray(),
                []),
            cancellationToken);
    }

    public async Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken)
    {
        var partner = await db.BusinessPartners.Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == request.PartnerId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("合作单位不存在或已停用。");
        if (!partner.Roles.Any(role => role.RoleType == request.RoleType))
        {
            throw new InvalidOperationException("合作单位未配置所选业务角色。");
        }

        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }

        if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(
                item => item.Id == request.ContractId && item.ProjectId == request.ProjectId && item.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("合同不存在或不属于所选项目。");
        }

        if (await db.ProjectPartners.AnyAsync(item =>
                item.ProjectId == request.ProjectId
                && item.BusinessPartnerId == request.PartnerId
                && item.RoleType == request.RoleType,
                cancellationToken))
        {
            return;
        }

        db.ProjectPartners.Add(new ProjectPartner
        {
            ProjectId = request.ProjectId,
            BusinessPartnerId = request.PartnerId,
            RoleType = request.RoleType,
            ContractId = request.ContractId,
            IsPrimary = request.IsPrimary
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(
        string? search,
        BusinessPartnerRoleType? role,
        CancellationToken cancellationToken)
    {
        var query = db.BusinessPartners.AsNoTracking()
            .Include(item => item.Roles)
            .Include(item => item.Contacts)
            .Include(item => item.ProjectLinks)
            .Where(item => item.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.PartnerNumber.Contains(term) || item.Name.Contains(term) || item.ShortName.Contains(term));
        }

        if (role.HasValue)
        {
            query = query.Where(item => item.Roles.Any(partnerRole => partnerRole.RoleType == role.Value));
        }

        var partners = await query.OrderBy(item => item.PartnerNumber).ToListAsync(cancellationToken);
        return partners.Select(ToDto).ToArray();
    }

    public async Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken)
    {
        var partner = await db.BusinessPartners.AsNoTracking()
            .Include(item => item.Roles)
            .Include(item => item.Contacts)
            .Include(item => item.ProjectLinks)
            .SingleOrDefaultAsync(item => item.Id == partnerId && item.IsActive, cancellationToken);
        return partner is null ? null : ToDto(partner);
    }

    private static void AddRoles(BusinessPartner partner, IEnumerable<PartnerRoleRequest> roles)
    {
        foreach (var role in roles.GroupBy(item => item.RoleType).Select(group => group.First()))
        {
            partner.Roles.Add(new BusinessPartnerRole
            {
                Partner = partner,
                RoleType = role.RoleType,
                TradeCategory = NormalizeOptional(role.TradeCategory),
                PricingRule = NormalizeOptional(role.PricingRule),
                SettlementTerms = NormalizeOptional(role.SettlementTerms)
            });
        }
    }

    private static BusinessPartnerDto ToDto(BusinessPartner partner) =>
        new(
            partner.Id,
            partner.PartnerNumber,
            partner.Name,
            partner.ShortName,
            partner.UnifiedSocialCreditCode,
            partner.Notes,
            partner.Roles.OrderBy(role => role.RoleType).Select(role => new PartnerRoleDto(role.RoleType, role.TradeCategory, role.PricingRule, role.SettlementTerms)).ToArray(),
            partner.Contacts.OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name).Select(contact => new PartnerContactDto(contact.Id, contact.Name, contact.Phone, contact.Email, contact.Address, contact.IsPrimary)).ToArray(),
            partner.ProjectLinks.Count(link => link.IsActive));

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
