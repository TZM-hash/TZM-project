using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Domain.Personnel;

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

    public static void ApplyActiveProfile(Person person, bool isActive, PersonnelEngagementHistory? currentAffiliation)
    {
        if (currentAffiliation is null)
        {
            if (!isActive)
            {
                SetActive(person.Employee, false);
                SetActive(person.ConstructionWorker, false);
            }

            return;
        }

        SetActive(person.Employee, isActive && currentAffiliation.Scope == PersonnelScope.Internal);
        SetActive(
            person.ConstructionWorker,
            isActive
            && currentAffiliation.Scope == PersonnelScope.External
            && currentAffiliation.ExternalType == ExternalPersonnelType.ConstructionCrew);
    }

    private static void SetActive(Employee? employee, bool isActive)
    {
        if (employee is null || employee.IsActive == isActive) return;
        employee.IsActive = isActive;
        employee.UpdatedAt = DateTimeOffset.UtcNow;
        employee.ConcurrencyStamp = Guid.NewGuid();
    }

    private static void SetActive(ConstructionWorker? worker, bool isActive)
    {
        if (worker is null || worker.IsActive == isActive) return;
        worker.IsActive = isActive;
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        worker.ConcurrencyStamp = Guid.NewGuid();
    }

    private static string Required(string value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("值不能为空。", parameterName)
        : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
