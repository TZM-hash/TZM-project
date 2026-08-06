using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Partners;

internal static class BusinessPartnerAutomaticNameSynchronizer
{
    public static async Task UpdateAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Guid> projectIds,
        string previousName,
        string previousShortName,
        string name,
        string shortName,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0
            || string.Equals(previousName, name, StringComparison.Ordinal)
            && string.Equals(previousShortName, shortName, StringComparison.Ordinal))
        {
            return;
        }

        var projects = await db.Projects
            .Where(item => projectIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            var changed = false;
            var contractorNames = ProjectGeneralContractors.Parse(project.GeneralContractorName)
                .Select(item =>
                {
                    if (MatchesPartnerName(item, previousName))
                    {
                        changed = true;
                        return name;
                    }
                    if (MatchesPartnerName(item, previousShortName))
                    {
                        changed = true;
                        return shortName;
                    }
                    return item;
                })
                .ToArray();
            if (changed)
            {
                project.GeneralContractorName = ProjectGeneralContractors.Serialize(contractorNames);
            }
        }
    }

    private static bool MatchesPartnerName(string sourceName, string partnerName) =>
        string.Equals(NormalizeLookupKey(sourceName), NormalizeLookupKey(partnerName), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLookupKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
}
