namespace EngineeringManager.Domain.Reminders;

public enum ReminderType
{
    ProjectMilestone = 1,
    UncollectedReceivable = 2,
    UnpaidPayable = 3,
    UninvoicedReceivable = 4,
    UnpaidPayroll = 5,
    OfflineSyncFailed = 6,
    ImportFailed = 7,
    ExportFailed = 8,
    BackupFailed = 9,
    CompanyCertificateExpiring = 10
}

public enum ReminderSeverity { Info = 1, Warning = 2, Critical = 3 }
public enum ReminderStatus { Unread = 1, Read = 2, Resolved = 3 }
