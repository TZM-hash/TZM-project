using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Partners;

public sealed class BusinessPartnerDirectorySynchronizer(ApplicationDbContext db) : IBusinessPartnerDirectorySynchronizer
{
    private static readonly SemaphoreSlim SynchronizationGate = new(1, 1);
    private static readonly string[] CrewNameFragments = ["班组", "施工队", "劳务队", "作业队"];
    private static readonly string[] CrewTradeSuffixes = ["钢筋工", "木工", "架子工", "瓦工", "泥工", "焊工", "电工", "水电工", "杂工", "油漆工", "涂料工", "安装工", "管工", "测量工"];
    private static readonly string[] CustomerNameFragments = ["甲方", "总包", "总承包", "业主", "建设单位", "发包人"];
    internal const string AutoGeneralContractorNote = "由项目总包名称自动同步";
    private const string AutoManagedGeneralContractorNote = AutoGeneralContractorNote + "（自动维护角色）";

    public async Task SynchronizeAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        await SynchronizationGate.WaitAsync(cancellationToken);
        try
        {
            await SynchronizeCoreAsync(projectId, cancellationToken);
        }
        finally
        {
            SynchronizationGate.Release();
        }
    }

    private async Task SynchronizeCoreAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var projectQuery = db.Projects.AsNoTracking();
        if (projectId.HasValue)
        {
            projectQuery = projectQuery.Where(item => item.Id == projectId.Value);
        }

        var projects = await projectQuery
            .Select(item => new ProjectSource(item.Id, item.GeneralContractorName))
            .ToListAsync(cancellationToken);
        if (projects.Count == 0 && projectId.HasValue)
        {
            return;
        }

        var projectIds = projects.Select(item => item.Id).ToArray();
        var partners = await db.BusinessPartners
            .Include(item => item.Roles)
            .Include(item => item.ProjectLinks)
            .ToListAsync(cancellationToken);
        var partnersById = partners.ToDictionary(item => item.Id);

        RemoveStaleAutomaticGeneralContractorLinks(projects, partners);

        var projectPartnerRoles = partners
            .SelectMany(partner => partner.ProjectLinks)
            .Where(item => projectIds.Contains(item.ProjectId) && !IsAutomaticGeneralContractorLink(item))
            .Select(item => new ProjectPartnerSource(item.BusinessPartnerId, item.RoleType))
            .ToArray();
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

                if (HasManualGeneralContractorReclassification(partner, project.Id))
                {
                    continue;
                }

                var roleManaged = partner.ProjectLinks.Any(IsAutoManagedGeneralContractorLink)
                    || EnsureRole(partner, BusinessPartnerRoleType.CustomerOrGeneralContractor);
                EnsureProjectLink(partner, project.Id, BusinessPartnerRoleType.CustomerOrGeneralContractor, roleManaged);
            }
        }

        foreach (var partner in partners.Where(item => item.Roles.Count == 0))
        {
            EnsureRole(partner, InferLegacyRole(partner));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static BusinessPartnerRoleType InferLegacyRole(BusinessPartner partner)
    {
        if (ContainsAny(partner.Name, CrewNameFragments)
            || ContainsAny(partner.ShortName, CrewNameFragments)
            || EndsWithAny(partner.Name, CrewTradeSuffixes)
            || EndsWithAny(partner.ShortName, CrewTradeSuffixes))
        {
            return BusinessPartnerRoleType.ConstructionCrew;
        }

        if (ContainsAny(partner.Name, CustomerNameFragments)
            || ContainsAny(partner.ShortName, CustomerNameFragments))
        {
            return BusinessPartnerRoleType.CustomerOrGeneralContractor;
        }

        return BusinessPartnerRoleType.MaterialSupplier;
    }

    private static bool ContainsAny(string? value, IEnumerable<string> keywords) =>
        !string.IsNullOrWhiteSpace(value)
        && keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool EndsWithAny(string? value, IEnumerable<string> keywords) =>
        !string.IsNullOrWhiteSpace(value)
        && keywords.Any(keyword => value.Trim().EndsWith(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool HasManualGeneralContractorReclassification(BusinessPartner partner, Guid projectId) =>
        partner.ProjectLinks.Any(item =>
            item.ProjectId == projectId
            && item.RoleType != BusinessPartnerRoleType.CustomerOrGeneralContractor
            && item.Notes?.Contains(AutoGeneralContractorNote, StringComparison.Ordinal) == true);

    private void RemoveStaleAutomaticGeneralContractorLinks(
        IReadOnlyCollection<ProjectSource> projects,
        IReadOnlyCollection<BusinessPartner> partners)
    {
        var desiredKeysByProject = projects.ToDictionary(
            item => item.Id,
            item => ProjectGeneralContractors.Parse(item.GeneralContractorName)
                .Select(NormalizeLookupKey)
                .Where(key => key.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var partner in partners)
        {
            var staleLinks = partner.ProjectLinks
                .Where(item => desiredKeysByProject.TryGetValue(item.ProjectId, out var desiredKeys)
                    && IsAutomaticGeneralContractorLink(item)
                    && !desiredKeys.Contains(NormalizeLookupKey(partner.Name))
                    && !desiredKeys.Contains(NormalizeLookupKey(partner.ShortName)))
                .ToArray();
            if (staleLinks.Length == 0)
            {
                continue;
            }

            var managedRole = staleLinks.Any(IsAutoManagedGeneralContractorLink);
            foreach (var staleLink in staleLinks)
            {
                if (HasUserData(staleLink))
                {
                    staleLink.IsActive = false;
                }
                else
                {
                    partner.ProjectLinks.Remove(staleLink);
                    db.ProjectPartners.Remove(staleLink);
                }
            }

            if (managedRole
                && !partner.ProjectLinks.Any(item => item.IsActive && item.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor)
                && partner.Roles.FirstOrDefault(item => item.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor) is { } role)
            {
                partner.Roles.Remove(role);
                db.BusinessPartnerRoles.Remove(role);
            }
        }
    }

    private static bool IsAutomaticGeneralContractorLink(ProjectPartner link) =>
        link.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor
        && link.Notes?.Contains(AutoGeneralContractorNote, StringComparison.Ordinal) == true;

    private static bool IsAutoManagedGeneralContractorLink(ProjectPartner link) =>
        string.Equals(link.Notes, AutoManagedGeneralContractorNote, StringComparison.Ordinal);

    private static bool HasUserData(ProjectPartner link) =>
        link.ContractId.HasValue
        || link.IsPrimary
        || (!string.Equals(link.Notes, AutoGeneralContractorNote, StringComparison.Ordinal)
            && !string.Equals(link.Notes, AutoManagedGeneralContractorNote, StringComparison.Ordinal));

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

    private bool EnsureRole(BusinessPartner partner, BusinessPartnerRoleType roleType)
    {
        if (partner.Roles.Any(item => item.RoleType == roleType))
        {
            return false;
        }

        var role = new BusinessPartnerRole
        {
            Partner = partner,
            RoleType = roleType
        };
        partner.Roles.Add(role);
        db.BusinessPartnerRoles.Add(role);
        return true;
    }

    private void EnsureProjectLink(BusinessPartner partner, Guid projectId, BusinessPartnerRoleType roleType, bool roleManaged)
    {
        var existing = partner.ProjectLinks.FirstOrDefault(item => item.ProjectId == projectId && item.RoleType == roleType);
        if (existing is not null)
        {
            if (IsAutomaticGeneralContractorLink(existing))
            {
                existing.IsActive = true;
                if (roleManaged)
                {
                    existing.Notes = AutoManagedGeneralContractorNote;
                }
            }
            return;
        }

        var link = new ProjectPartner
        {
            ProjectId = projectId,
            Partner = partner,
            RoleType = roleType,
            Notes = roleManaged ? AutoManagedGeneralContractorNote : AutoGeneralContractorNote
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
