namespace EngineeringManager.Application.Projects;

public static class ProjectContactDisplay
{
    private static readonly char[] ContactSeparators = ['，', ',', '、', '；', ';', '\r', '\n'];

    public static string Format(string? contact, string? phone)
    {
        var contactParts = string.IsNullOrWhiteSpace(contact)
            ? Array.Empty<string>()
            : contact.Split(ContactSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (contactParts.Length == 0 && string.IsNullOrWhiteSpace(phone)) return "未设置";

        var contactLabel = contactParts.Length == 0 ? "-" : string.Join("、", contactParts);
        var phoneLabel = string.IsNullOrWhiteSpace(phone) ? "-" : phone.Trim();
        return $"{contactLabel} · {phoneLabel}";
    }
}
