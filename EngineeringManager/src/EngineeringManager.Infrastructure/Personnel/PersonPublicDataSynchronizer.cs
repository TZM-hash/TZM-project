using EngineeringManager.Infrastructure.Data;

namespace EngineeringManager.Infrastructure.Personnel;

public static class PersonPublicDataSynchronizer
{
    public static void Apply(
        Person person,
        string name,
        string? phone,
        string? identityNumber,
        string? bankAccountNumber,
        string? bankName,
        string? notes,
        bool isActive)
    {
        person.Name = Required(name, nameof(name));
        person.Phone = Optional(phone);
        person.IdentityNumber = Optional(identityNumber);
        person.IdentityNumberNormalized = NormalizeIdentityNumber(identityNumber);
        person.BankAccountNumber = Optional(bankAccountNumber);
        person.BankName = Optional(bankName);
        person.Notes = Optional(notes);
        person.IsActive = isActive;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        person.ConcurrencyStamp = Guid.NewGuid();

        if (person.Employee is not null)
        {
            person.Employee.Name = person.Name;
            person.Employee.Phone = person.Phone;
            person.Employee.IdentityNumber = person.IdentityNumber;
            person.Employee.BankAccountNumber = person.BankAccountNumber;
            person.Employee.BankName = person.BankName;
            person.Employee.Notes = person.Notes;
            person.Employee.IsActive = isActive;
            person.Employee.UpdatedAt = person.UpdatedAt;
            person.Employee.ConcurrencyStamp = Guid.NewGuid();
        }

        if (person.ConstructionWorker is not null)
        {
            person.ConstructionWorker.Name = person.Name;
            person.ConstructionWorker.Phone = person.Phone;
            person.ConstructionWorker.IdentityNumber = person.IdentityNumber;
            person.ConstructionWorker.BankAccountNumber = person.BankAccountNumber;
            person.ConstructionWorker.BankName = person.BankName;
            person.ConstructionWorker.Notes = person.Notes;
            person.ConstructionWorker.IsActive = isActive;
            person.ConstructionWorker.UpdatedAt = person.UpdatedAt;
            person.ConstructionWorker.ConcurrencyStamp = Guid.NewGuid();
        }
    }

    public static string? NormalizeIdentityNumber(string? value)
    {
        var normalized = Optional(value);
        return normalized is null
            ? null
            : normalized.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
    }

    private static string Required(string value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("值不能为空。", parameterName)
        : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
