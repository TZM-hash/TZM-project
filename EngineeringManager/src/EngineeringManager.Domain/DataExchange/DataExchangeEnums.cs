namespace EngineeringManager.Domain.DataExchange;

public enum ExportDataset
{
    ProjectOverview = 1,
    Projects = 2,
    Contracts = 3,
    Partners = 4,
    Employees = 5,
    Payroll = 6,
    Collections = 7,
    Payments = 8,
    Invoices = 9,
    Accounts = 10,
    StageResults = 11,
    Companies = 12,
    CompanyAccounts = 13,
    CompanyCertificates = 14,
    Equipment = 15,
    EquipmentLeases = 16,
    EquipmentUsages = 17,
    EquipmentPeriods = 18,
    EquipmentSettlements = 19,
    EmployeeCertificates = 20
}

public enum ExportFieldDataType { Text = 1, Number = 2, Date = 3, Boolean = 4 }
public enum ExportTemplateScope { Personal = 1, Shared = 2 }
public enum DataExchangeTaskStatus { Pending = 1, PreviewReady = 2, Running = 3, Completed = 4, Failed = 5 }

public sealed record ExportFieldDefinition(string Key, string Label, ExportFieldDataType DataType, bool IsDefault);
