using System.Globalization;
using System.Text.Json;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Data;

public sealed record LegacyDataRepairMapping(
    string EntityType,
    Guid EntityId,
    string FieldName,
    string OldValue,
    string NewValue);

public sealed record LegacyDataRepairResult(IReadOnlyList<LegacyDataRepairMapping> Mappings)
{
    public int TotalChanges => Mappings.Count;

    public IReadOnlyDictionary<string, int> Counts => Mappings
        .GroupBy(item => item.EntityType, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

public sealed class LegacyDataRepairService(ApplicationDbContext db)
{
    private const string GeneratedPartnerNotePrefix = "从旧资料原始合作单位补建";
    private const string GeneratedPartnerRepairMarker = "系统已复核自动合作单位";
    private const string ProjectEntity = "Project";
    private const string ContractEntity = "Contract";
    private const string QuantityEntity = "ContractLineItem";
    private const string EmployeeEntity = "Employee";
    private const string PartnerEntity = "BusinessPartner";
    private const string InvoiceEntity = "FinanceInvoice";
    private const string CompanyEntity = "LegalEntity";
    private const string AccountEntity = "FinancialAccount";
    private const string ReminderEntity = "ReminderItem";

    public async Task<LegacyDataRepairResult> RepairAsync(CancellationToken cancellationToken)
    {
        var allProjects = await db.Projects.ToListAsync(cancellationToken);
        var allContracts = await db.Contracts.ToListAsync(cancellationToken);
        var allQuantities = await db.ContractLineItems.ToListAsync(cancellationToken);
        var allEmployees = await db.Employees.ToListAsync(cancellationToken);
        var allPartners = await db.BusinessPartners.ToListAsync(cancellationToken);
        var allInvoices = await db.FinanceInvoices.ToListAsync(cancellationToken);
        var allCompanies = await db.LegalEntities.ToListAsync(cancellationToken);
        var allAccounts = await db.FinancialAccounts.ToListAsync(cancellationToken);
        var allReminders = await db.ReminderItems.ToListAsync(cancellationToken);
        var projectNames = allProjects.ToDictionary(item => item.Id, item => item.Name);

        var projects = allProjects.Where(item => IsLegacyNumber(item.ProjectNumber))
            .OrderBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var contracts = allContracts.Where(item => IsLegacyNumber(item.ContractNumber))
            .OrderBy(item => item.ProjectId).ThenBy(item => item.ContractNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var quantities = allQuantities.Where(item => IsLegacyNumber(item.Code))
            .OrderBy(item => item.ContractId).ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var employees = allEmployees.Where(item => IsLegacyNumber(item.EmployeeNumber))
            .OrderBy(item => item.EmployeeNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var partners = allPartners.Where(item => IsLegacyNumber(item.PartnerNumber))
            .OrderBy(item => item.PartnerNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var invoices = allInvoices.Where(item => IsLegacyNumber(item.InvoiceNumber))
            .OrderBy(item => item.InvoiceNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var companies = allCompanies.Where(item => IsGeneratedCompanyCode(item.Code))
            .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var contractsWithGeneratedNames = allContracts.Where(item => IsGeneratedContractName(item, projectNames))
            .OrderBy(item => item.ProjectId).ThenBy(item => item.ContractNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var quantitiesWithGeneratedNames = allQuantities.Where(IsGeneratedQuantityName)
            .OrderBy(item => item.ContractId).ThenBy(item => item.SortOrder).ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var partnersWithGeneratedNames = allPartners.Where(item => item.Name == "待确认合作单位（旧资料补导）").ToArray();
        var partnersWithGeneratedSourceNames = allPartners.Where(item =>
                item.Notes?.StartsWith(GeneratedPartnerNotePrefix, StringComparison.Ordinal) == true
                && item.Notes.Contains(GeneratedPartnerRepairMarker, StringComparison.Ordinal) == false)
            .OrderBy(item => item.PartnerNumber, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        var companiesWithGeneratedNames = allCompanies.Where(item => item.Name == "待确认签约公司（旧资料补导）").ToArray();
        var accountsWithGeneratedNames = allAccounts.Where(item => item.AccountName == "待确认账户（旧资料补导）").ToArray();

        var mappings = new List<LegacyDataRepairMapping>();
        var projectNumbers = BuildGlobalMappings(
            projects,
            allProjects.Except(projects).Select(item => item.ProjectNumber),
            item => item.Id,
            item => item.ProjectNumber,
            "XM",
            4,
            ProjectEntity,
            nameof(Project.ProjectNumber),
            mappings);

        var finalProjectNumbers = allProjects.ToDictionary(
            item => item.Id,
            item => projectNumbers.GetValueOrDefault(item.Id, item.ProjectNumber));
        var contractNumbers = new Dictionary<Guid, string>();
        foreach (var group in contracts.GroupBy(item => item.ProjectId).OrderBy(group => group.Key))
        {
            var targetIds = group.Select(item => item.Id).ToHashSet();
            var used = allContracts.Where(item => item.ProjectId == group.Key && !targetIds.Contains(item.Id))
                .Select(item => item.ContractNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sequence = 1;
            foreach (var contract in group)
            {
                string candidate;
                do candidate = $"{finalProjectNumbers[group.Key]}-C{sequence++:00}";
                while (!used.Add(candidate));
                contractNumbers[contract.Id] = candidate;
                mappings.Add(new LegacyDataRepairMapping(ContractEntity, contract.Id, nameof(Contract.ContractNumber), contract.ContractNumber, candidate));
            }
        }

        var quantityNumbers = new Dictionary<Guid, string>();
        foreach (var group in quantities.GroupBy(item => item.ContractId).OrderBy(group => group.Key))
        {
            var targetIds = group.Select(item => item.Id).ToHashSet();
            var used = allQuantities.Where(item => item.ContractId == group.Key && !targetIds.Contains(item.Id))
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sequence = 1;
            foreach (var quantity in group)
            {
                string candidate;
                do candidate = $"QD{sequence++:000}";
                while (!used.Add(candidate));
                quantityNumbers[quantity.Id] = candidate;
                mappings.Add(new LegacyDataRepairMapping(QuantityEntity, quantity.Id, nameof(ContractLineItem.Code), quantity.Code, candidate));
            }
        }

        var employeeNumbers = BuildGlobalMappings(
            employees,
            allEmployees.Except(employees).Select(item => item.EmployeeNumber),
            item => item.Id,
            item => item.EmployeeNumber,
            "YG",
            4,
            EmployeeEntity,
            nameof(Employee.EmployeeNumber),
            mappings);
        var partnerNumbers = BuildGlobalMappings(
            partners,
            allPartners.Except(partners).Select(item => item.PartnerNumber),
            item => item.Id,
            item => item.PartnerNumber,
            "HZ",
            4,
            PartnerEntity,
            nameof(BusinessPartner.PartnerNumber),
            mappings);
        var invoiceNumbers = BuildGlobalMappings(
            invoices,
            allInvoices.Except(invoices).Select(item => item.InvoiceNumber),
            item => item.Id,
            item => item.InvoiceNumber,
            "FP",
            6,
            InvoiceEntity,
            nameof(FinanceInvoice.InvoiceNumber),
            mappings);
        var companyNumbers = BuildGlobalMappings(
            companies,
            allCompanies.Except(companies).Select(item => item.Code),
            item => item.Id,
            item => item.Code,
            "GS",
            4,
            CompanyEntity,
            nameof(LegalEntity.Code),
            mappings);

        var contractNames = contractsWithGeneratedNames.ToDictionary(item => item.Id, _ => "主合同（待确认）");
        foreach (var contract in contractsWithGeneratedNames)
            mappings.Add(new LegacyDataRepairMapping(ContractEntity, contract.Id, nameof(Contract.Name), contract.Name, contractNames[contract.Id]));

        var quantityNames = new Dictionary<Guid, string>();
        foreach (var group in quantitiesWithGeneratedNames.GroupBy(item => item.ContractId).OrderBy(group => group.Key))
        {
            var sequence = 1;
            foreach (var quantity in group)
            {
                var name = $"待确认工程量{sequence++}";
                quantityNames[quantity.Id] = name;
                mappings.Add(new LegacyDataRepairMapping(QuantityEntity, quantity.Id, nameof(ContractLineItem.Name), quantity.Name, name));
            }
        }

        foreach (var partner in partnersWithGeneratedNames)
            mappings.Add(new LegacyDataRepairMapping(PartnerEntity, partner.Id, nameof(BusinessPartner.Name), partner.Name, "待确认合作单位"));
        var generatedPartnerNames = partnersWithGeneratedSourceNames.ToDictionary(
            item => item.Id,
            item => BuildShortGeneratedPartnerName(partnerNumbers.GetValueOrDefault(item.Id, item.PartnerNumber)));
        foreach (var partner in partnersWithGeneratedSourceNames)
        {
            var shortName = generatedPartnerNames[partner.Id];
            if (!string.Equals(partner.Name, shortName, StringComparison.Ordinal))
                mappings.Add(new LegacyDataRepairMapping(PartnerEntity, partner.Id, nameof(BusinessPartner.Name), partner.Name, shortName));
            if (!string.Equals(partner.ShortName, shortName, StringComparison.Ordinal))
                mappings.Add(new LegacyDataRepairMapping(PartnerEntity, partner.Id, nameof(BusinessPartner.ShortName), partner.ShortName, shortName));
            var repairedNotes = AppendGeneratedPartnerRepairMarker(partner.Notes);
            if (!string.Equals(partner.Notes, repairedNotes, StringComparison.Ordinal))
                mappings.Add(new LegacyDataRepairMapping(PartnerEntity, partner.Id, nameof(BusinessPartner.Notes), partner.Notes ?? string.Empty, repairedNotes));
        }
        foreach (var company in companiesWithGeneratedNames)
            mappings.Add(new LegacyDataRepairMapping(CompanyEntity, company.Id, nameof(LegalEntity.Name), company.Name, "待确认签约公司"));
        foreach (var account in accountsWithGeneratedNames)
            mappings.Add(new LegacyDataRepairMapping(AccountEntity, account.Id, nameof(FinancialAccount.AccountName), account.AccountName, "待确认账户"));

        var reminderMessages = new Dictionary<Guid, string>();
        foreach (var reminder in allReminders.Where(item => string.Equals(item.SourceType, "Project", StringComparison.Ordinal)
                     && item.DeduplicationKey.StartsWith("project-", StringComparison.Ordinal)
                     && Guid.TryParse(item.SourceId, out _)))
        {
            if (!Guid.TryParse(reminder.SourceId, out var projectId) || !projectNames.TryGetValue(projectId, out var projectName))
                continue;

            var projectNumber = finalProjectNumbers[projectId];
            var label = $"{projectNumber} · {projectName}";
            var message = reminder.Type switch
            {
                ReminderType.UncollectedReceivable => $"{label} 未收款 {reminder.Amount.GetValueOrDefault():N2}",
                ReminderType.UnpaidPayable => $"{label} 未付款 {reminder.Amount.GetValueOrDefault():N2}",
                ReminderType.UninvoicedReceivable => $"{label} 未开票 {reminder.Amount.GetValueOrDefault():N2}",
                _ => null
            };
            if (message is not null && !string.Equals(reminder.Message, message, StringComparison.Ordinal))
            {
                reminderMessages[reminder.Id] = message;
                mappings.Add(new LegacyDataRepairMapping(ReminderEntity, reminder.Id, nameof(ReminderItem.Message), reminder.Message, message));
            }
        }

        if (mappings.Count == 0)
            return new LegacyDataRepairResult([]);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in projects) item.ProjectNumber = TemporaryNumber("P", item.Id);
        foreach (var item in contracts) item.ContractNumber = TemporaryNumber("C", item.Id);
        foreach (var item in quantities) item.Code = TemporaryNumber("Q", item.Id);
        foreach (var item in employees) item.EmployeeNumber = TemporaryNumber("E", item.Id);
        foreach (var item in partners) item.PartnerNumber = TemporaryNumber("B", item.Id);
        foreach (var item in invoices) item.InvoiceNumber = TemporaryNumber("I", item.Id);
        foreach (var item in companies) item.Code = TemporaryNumber("L", item.Id);
        if (projects.Length + contracts.Length + quantities.Length + employees.Length + partners.Length + invoices.Length + companies.Length > 0)
            await db.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var item in projects)
        {
            item.ProjectNumber = projectNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in contracts)
        {
            item.ContractNumber = contractNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in quantities)
        {
            item.Code = quantityNumbers[item.Id];
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in employees)
        {
            item.EmployeeNumber = employeeNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in partners)
        {
            item.PartnerNumber = partnerNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in invoices)
        {
            item.InvoiceNumber = invoiceNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in companies)
        {
            item.Code = companyNumbers[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in contractsWithGeneratedNames)
        {
            item.Name = contractNames[item.Id];
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in quantitiesWithGeneratedNames)
        {
            item.Name = quantityNames[item.Id];
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in partnersWithGeneratedNames)
        {
            item.Name = "待确认合作单位";
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in partnersWithGeneratedSourceNames)
        {
            item.Name = generatedPartnerNames[item.Id];
            item.ShortName = generatedPartnerNames[item.Id];
            item.Notes = AppendGeneratedPartnerRepairMarker(item.Notes);
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in companiesWithGeneratedNames)
        {
            item.Name = "待确认签约公司";
            item.UpdatedAt = now;
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var item in accountsWithGeneratedNames)
        {
            item.AccountName = "待确认账户";
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        foreach (var reminder in allReminders)
        {
            if (reminderMessages.TryGetValue(reminder.Id, out var message))
                reminder.Message = message;
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = "system",
            UserName = "系统维护",
            Action = "RepairLegacyGeneratedData",
            EntityType = nameof(LegacyDataRepairService),
            EntityId = Guid.Empty.ToString(),
            Reason = "缩短旧资料自动生成编号和名称",
            AfterJson = JsonSerializer.Serialize(new LegacyDataRepairResult(mappings))
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LegacyDataRepairResult(mappings);
    }

    private static Dictionary<Guid, string> BuildGlobalMappings<T>(
        IReadOnlyList<T> targets,
        IEnumerable<string> existingNumbers,
        Func<T, Guid> idSelector,
        Func<T, string> numberSelector,
        string prefix,
        int digits,
        string entityType,
        string fieldName,
        List<LegacyDataRepairMapping> mappings)
    {
        var used = existingNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<Guid, string>();
        var sequence = 1;
        foreach (var item in targets)
        {
            string candidate;
            do candidate = prefix + sequence++.ToString($"D{digits}", CultureInfo.InvariantCulture);
            while (!used.Add(candidate));
            var id = idSelector(item);
            result[id] = candidate;
            mappings.Add(new LegacyDataRepairMapping(entityType, id, fieldName, numberSelector(item), candidate));
        }
        return result;
    }

    private static bool IsLegacyNumber(string value) =>
        value.StartsWith("OLD-", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeneratedCompanyCode(string value) =>
        IsLegacyNumber(value) || value.StartsWith("OFFICIAL-", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeneratedContractName(Contract item, Dictionary<Guid, string> projectNames) =>
        projectNames.TryGetValue(item.ProjectId, out var projectName)
        && string.Equals(item.Name, $"{projectName}-原始主合同（待确认）", StringComparison.Ordinal);

    private static bool IsGeneratedQuantityName(ContractLineItem item) =>
        item.Name.StartsWith("待补工程量-OLD-", StringComparison.Ordinal)
        && item.Notes?.Contains("工程量名称原文为空", StringComparison.Ordinal) == true;

    private static string BuildShortGeneratedPartnerName(string partnerNumber)
    {
        const string prefix = "HZ";
        var suffix = partnerNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && partnerNumber[prefix.Length..].All(char.IsDigit)
                ? partnerNumber[prefix.Length..]
                : string.Empty;
        return suffix.Length == 0 ? "待确认单位" : $"待确认单位{suffix}";
    }

    private static string AppendGeneratedPartnerRepairMarker(string? notes)
    {
        var current = notes ?? string.Empty;
        return current.Contains(GeneratedPartnerRepairMarker, StringComparison.Ordinal)
            ? current
            : $"{current.TrimEnd()}；{GeneratedPartnerRepairMarker}";
    }

    private static string TemporaryNumber(string entityPrefix, Guid id) =>
        $"~{entityPrefix}{id:N}";
}
