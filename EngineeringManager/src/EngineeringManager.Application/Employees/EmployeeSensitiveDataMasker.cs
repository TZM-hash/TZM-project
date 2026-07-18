namespace EngineeringManager.Application.Employees;

public static class EmployeeSensitiveDataMasker
{
    public static string? MaskIdentityNumber(string? value) => Mask(value, 3, 4);

    public static string? MaskBankAccountNumber(string? value) => Mask(value, 0, 4);

    private static string? Mask(string? value, int visiblePrefixLength, int visibleSuffixLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length <= visiblePrefixLength + visibleSuffixLength)
        {
            return new string('*', normalized.Length);
        }

        return string.Concat(
            normalized.AsSpan(0, visiblePrefixLength),
            new string('*', normalized.Length - visiblePrefixLength - visibleSuffixLength),
            normalized.AsSpan(normalized.Length - visibleSuffixLength));
    }
}
