using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                IsPrimary = contact.IsPrimary,
                Notes = NormalizeOptional(contact.Notes)
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

    public async Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        var partner = await db.BusinessPartners.Include(item => item.Roles).Include(item => item.Contacts).Include(item => item.ProjectLinks)
            .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken) ?? throw new InvalidOperationException("合作单位不存在。");
        if (partner.ConcurrencyStamp != request.ConcurrencyStamp) throw new DbUpdateConcurrencyException("合作单位资料已被其他用户修改，请刷新后重试。");
        var number = NormalizeRequired(request.PartnerNumber, nameof(request.PartnerNumber));
        if (await db.BusinessPartners.AnyAsync(item => item.Id != request.Id && item.PartnerNumber == number, cancellationToken)) throw new InvalidOperationException($"合作单位编号已存在：{number}");
        var creditCode = NormalizeOptional(request.UnifiedSocialCreditCode);
        if (creditCode is not null && await db.BusinessPartners.AnyAsync(item => item.Id != request.Id && item.UnifiedSocialCreditCode == creditCode, cancellationToken)) throw new InvalidOperationException("统一社会信用代码已存在。");
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var before = Snapshot(partner);
        partner.PartnerNumber = number;
        partner.Name = NormalizeRequired(request.Name, nameof(request.Name));
        partner.ShortName = NormalizeRequired(request.ShortName, nameof(request.ShortName));
        partner.UnifiedSocialCreditCode = creditCode;
        partner.Notes = NormalizeOptional(request.Notes);
        partner.IsActive = request.IsActive;
        partner.UpdatedAt = DateTimeOffset.UtcNow;
        db.Entry(partner).Property(item => item.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
        partner.ConcurrencyStamp = Guid.NewGuid();
        var role = partner.Roles.FirstOrDefault(item => item.RoleType == request.Role.RoleType);
        if (role is null)
        {
            role = new BusinessPartnerRole { Partner = partner, RoleType = request.Role.RoleType };
            partner.Roles.Add(role);
            db.BusinessPartnerRoles.Add(role);
        }
        role.TradeCategory = NormalizeOptional(request.Role.TradeCategory); role.PricingRule = NormalizeOptional(request.Role.PricingRule); role.SettlementTerms = NormalizeOptional(request.Role.SettlementTerms);
        if (request.PrimaryContact is not null && !string.IsNullOrWhiteSpace(request.PrimaryContact.Name))
        {
            var contact = partner.Contacts.FirstOrDefault(item => item.IsPrimary);
            if (contact is null)
            {
                contact = new PartnerContact { Partner = partner, IsPrimary = true };
                partner.Contacts.Add(contact);
                db.PartnerContacts.Add(contact);
            }
            contact.Name = NormalizeRequired(request.PrimaryContact.Name, nameof(request.PrimaryContact.Name)); contact.Phone = NormalizeOptional(request.PrimaryContact.Phone); contact.Email = NormalizeOptional(request.PrimaryContact.Email); contact.Address = NormalizeOptional(request.PrimaryContact.Address); contact.Notes = NormalizeOptional(request.PrimaryContact.Notes);
        }
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "UpdateBusinessPartner", EntityType = nameof(BusinessPartner), EntityId = partner.Id.ToString(), Reason = reason, BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(Snapshot(partner)) });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(partner);
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

        var link = new ProjectPartner
        {
            ProjectId = request.ProjectId,
            BusinessPartnerId = request.PartnerId,
            RoleType = request.RoleType,
            ContractId = request.ContractId,
            IsPrimary = request.IsPrimary,
            Notes = NormalizeOptional(request.Notes)
        };
        db.ProjectPartners.Add(link);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "LinkPartnerToProject",
            EntityType = nameof(ProjectPartner),
            EntityId = link.Id.ToString(),
            RelatedProjectId = request.ProjectId.ToString(),
            Reason = "关联项目合作单位",
            AfterJson = JsonSerializer.Serialize(new
            {
                link.ProjectId,
                link.BusinessPartnerId,
                link.RoleType,
                link.ContractId,
                link.IsPrimary,
                link.Notes
            })
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
        foreach (var term in SearchTerms.Parse(search))
        {
            var parsedRoleFilter = ParseRole(term);
            query = query.Where(item =>
                item.PartnerNumber.Contains(term)
                || item.Name.Contains(term)
                || item.ShortName.Contains(term)
                || (item.UnifiedSocialCreditCode != null && item.UnifiedSocialCreditCode.Contains(term))
                || (item.Notes != null && item.Notes.Contains(term))
                || item.Roles.Any(partnerRole =>
                    (parsedRoleFilter.HasValue && partnerRole.RoleType == parsedRoleFilter.Value)
                    || (partnerRole.TradeCategory != null && partnerRole.TradeCategory.Contains(term))
                    || (partnerRole.PricingRule != null && partnerRole.PricingRule.Contains(term))
                    || (partnerRole.SettlementTerms != null && partnerRole.SettlementTerms.Contains(term)))
                || item.Contacts.Any(contact =>
                    contact.Name.Contains(term)
                    || (contact.Phone != null && contact.Phone.Contains(term))
                    || (contact.Email != null && contact.Email.Contains(term))
                    || (contact.Address != null && contact.Address.Contains(term))
                    || (contact.Notes != null && contact.Notes.Contains(term)))
                || item.ProjectLinks.Any(link =>
                    (link.Notes != null && link.Notes.Contains(term))
                    || (link.Project != null && (link.Project.ProjectNumber.Contains(term) || link.Project.Name.Contains(term)))));
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
            partner.Contacts.OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name).Select(contact => new PartnerContactDto(contact.Id, contact.Name, contact.Phone, contact.Email, contact.Address, contact.IsPrimary, contact.Notes)).ToArray(),
            partner.ProjectLinks.Count(link => link.IsActive),
            partner.IsActive,
            partner.ConcurrencyStamp);

    private static object Snapshot(BusinessPartner item) => new { item.PartnerNumber, item.Name, item.ShortName, item.UnifiedSocialCreditCode, item.Notes, item.IsActive, Roles = item.Roles.Select(role => new { role.RoleType, role.TradeCategory, role.PricingRule, role.SettlementTerms }).ToArray(), Contacts = item.Contacts.Select(contact => new { contact.Name, contact.Phone, contact.Email, contact.Address, contact.IsPrimary, contact.Notes }).ToArray() };

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BusinessPartnerRoleType? ParseRole(string term) => term switch
    {
        "甲方/总包" or "甲方" or "总包" => BusinessPartnerRoleType.CustomerOrGeneralContractor,
        "施工班组" or "班组" => BusinessPartnerRoleType.ConstructionCrew,
        "材料供应商" or "材料" => BusinessPartnerRoleType.MaterialSupplier,
        "零星供应商" or "零星" => BusinessPartnerRoleType.MiscellaneousSupplier,
        _ => Enum.TryParse<BusinessPartnerRoleType>(term, true, out var value) ? value : null
    };
}
