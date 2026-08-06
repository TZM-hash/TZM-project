using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Partners;

public sealed class BusinessPartnerDirectorySynchronizer(ApplicationDbContext db) : IBusinessPartnerDirectorySynchronizer
{
    public async Task SynchronizeAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var projectQuery = db.Projects.AsNoTracking();
        if (projectId.HasValue)
        {
            projectQuery = projectQuery.Where(item => item.Id == projectId.Value);
        }

        var projects = await projectQuery
            .Select(item => new ProjectSource(item.Id, item.GeneralContractorName))
            .ToListAsync(cancellationToken);
        if (projects.Count == 0)
        {
            return;
        }

        var projectIds = projects.Select(item => item.Id).ToArray();
        var partners = await db.BusinessPartners
            .Include(item => item.Roles)
            .Include(item => item.ProjectLinks)
            .ToListAsync(cancellationToken);
        var partnersById = partners.ToDictionary(item => item.Id);

        var projectPartnerRoles = await db.ProjectPartners.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId))
            .Select(item => new ProjectPartnerSource(item.BusinessPartnerId, item.RoleType))
            .ToListAsync(cancellationToken);
        foreach (var source in projectPartnerRoles)
        {
            if (partnersById.TryGetValue(source.BusinessPartnerId, out var partner))
            {
                EnsureRole(partner, source.RoleType);
            }
        }

        var crewPartnerIds = await db.ProjectConstructionRecords.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId)
                && item.RecordType == ProjectConstructionRecordType.ConstructionCrew
                && item.CrewBusinessPartnerId.HasValue)
            .Select(item => item.CrewBusinessPartnerId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var crewPartnerId in crewPartnerIds)
        {
            if (partnersById.TryGetValue(crewPartnerId, out var partner))
            {
                EnsureRole(partner, BusinessPartnerRoleType.ConstructionCrew);
            }
        }

        var nameLookup = BuildNameLookup(partners);
        var usedNumbers = partners.Select(item => item.PartnerNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            foreach (var contractorName in ProjectGeneralContractors.Parse(project.GeneralContractorName))
            {
                var lookupKey = NormalizeLookupKey(contractorName);
                if (nameLookup.TryGetValue(lookupKey, out var match) && match is null)
                {
                    continue;
                }

                var partner = match;
                if (partner is null)
                {
                    var name = Limit(contractorName.Trim(), 200);
                    var shortName = Limit(ProjectGeneralContractors.ToShortName(name), 100);
                    if (string.IsNullOrWhiteSpace(shortName))
                    {
                        shortName = name;
                    }

                    partner = new BusinessPartner
                    {
                        PartnerNumber = NextPartnerNumber(usedNumbers),
                        Name = name,
                        ShortName = shortName
                    };
                    partners.Add(partner);
                    partnersById.Add(partner.Id, partner);
                    db.BusinessPartners.Add(partner);
                    AddLookupKey(nameLookup, contractorName, partner);
                    AddLookupKey(nameLookup, partner.Name, partner);
                    AddLookupKey(nameLookup, partner.ShortName, partner);
                }

                EnsureRole(partner, BusinessPartnerRoleType.CustomerOrGeneralContractor);
                EnsureProjectLink(partner, project.Id, BusinessPartnerRoleType.CustomerOrGeneralContractor);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, BusinessPartner?> BuildNameLookup(IEnumerable<BusinessPartner> partners)
    {
        var lookup = new Dictionary<string, BusinessPartner?>(StringComparer.OrdinalIgnoreCase);
        foreach (var partner in partners)
        {
            AddLookupKey(lookup, partner.Name, partner);
            AddLookupKey(lookup, partner.ShortName, partner);
        }

        return lookup;
    }

    private static void AddLookupKey(Dictionary<string, BusinessPartner?> lookup, string? value, BusinessPartner partner)
    {
        var key = NormalizeLookupKey(value);
        if (key.Length == 0)
        {
            return;
        }

        if (lookup.TryGetValue(key, out var existing) && existing?.Id != partner.Id)
        {
            lookup[key] = null;
            return;
        }

        lookup[key] = partner;
    }

    private static string NormalizeLookupKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());

    private void EnsureRole(BusinessPartner partner, BusinessPartnerRoleType roleType)
    {
        if (partner.Roles.Any(item => item.RoleType == roleType))
        {
            return;
        }

        var role = new BusinessPartnerRole
        {
            Partner = partner,
            RoleType = roleType
        };
        partner.Roles.Add(role);
        db.BusinessPartnerRoles.Add(role);
    }

    private void EnsureProjectLink(BusinessPartner partner, Guid projectId, BusinessPartnerRoleType roleType)
    {
        if (partner.ProjectLinks.Any(item => item.ProjectId == projectId && item.RoleType == roleType))
        {
            return;
        }

        var link = new ProjectPartner
        {
            ProjectId = projectId,
            Partner = partner,
            RoleType = roleType,
            Notes = "由项目总包名称自动同步"
        };
        partner.ProjectLinks.Add(link);
        db.ProjectPartners.Add(link);
    }

    private static string NextPartnerNumber(HashSet<string> usedNumbers)
    {
        for (var number = 1; number <= 9999; number++)
        {
            var candidate = $"HZ{number:0000}";
            if (usedNumbers.Add(candidate))
            {
                return candidate;
            }
        }

        string fallback;
        do
        {
            fallback = $"HZ{Guid.NewGuid():N}"[..18];
        }
        while (!usedNumbers.Add(fallback));
        return fallback;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record ProjectSource(Guid Id, string? GeneralContractorName);
    private sealed record ProjectPartnerSource(Guid BusinessPartnerId, BusinessPartnerRoleType RoleType);
}
