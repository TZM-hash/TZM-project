using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Web.Presentation;

public sealed record ProjectRelatedPartyDisplay(string Name, IReadOnlyList<string> Roles, string? Notes);

public static class ProjectRelatedPartyBuilder
{
    public static IReadOnlyList<ProjectRelatedPartyDisplay> Build(
        string? generalContractorName,
        IEnumerable<ProjectPartnerLinkDto> partners,
        IEnumerable<ProjectConstructionRecordDto> constructionRecords)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        Add(entries, generalContractorName, "总包", null, 0, 0);

        foreach (var record in constructionRecords.Where(item => item.RecordType == ProjectConstructionRecordType.ConstructionCrew))
            Add(entries, record.SubjectLabel, "施工班组", null, 1, 2);

        foreach (var partner in partners.Where(item => item.IsActive))
        {
            var displayRole = partner.RoleType.ToChinese();
            Add(entries, partner.PartnerName, displayRole, partner.Notes, PartyOrder(partner.RoleType), RoleOrder(displayRole));
        }

        return entries.Values
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectRelatedPartyDisplay(
                item.Name,
                item.Roles.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key).ToArray(),
                item.Notes))
            .ToArray();
    }

    private static void Add(Dictionary<string, Entry> entries, string? name, string role, string? notes, int displayOrder, int roleOrder)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName)) return;
        if (!entries.TryGetValue(normalizedName, out var entry))
        {
            entry = new Entry(normalizedName, displayOrder);
            entries.Add(normalizedName, entry);
        }
        entry.DisplayOrder = Math.Min(entry.DisplayOrder, displayOrder);
        entry.Roles.TryAdd(role, roleOrder);
        if (!string.IsNullOrWhiteSpace(notes)) entry.Notes = notes.Trim();
    }

    private static int PartyOrder(BusinessPartnerRoleType role) => role switch
    {
        BusinessPartnerRoleType.CustomerOrGeneralContractor => 0,
        BusinessPartnerRoleType.ConstructionCrew => 1,
        _ => 2
    };

    private static int RoleOrder(string role) => role switch
    {
        "总包" => 0,
        "甲方/总包" => 1,
        "施工班组" => 2,
        "材料供应商" => 3,
        "零星供应商" => 4,
        _ => 5
    };

    private sealed class Entry(string name, int displayOrder)
    {
        public string Name { get; } = name;
        public int DisplayOrder { get; set; } = displayOrder;
        public Dictionary<string, int> Roles { get; } = new(StringComparer.Ordinal);
        public string? Notes { get; set; }
    }
}
