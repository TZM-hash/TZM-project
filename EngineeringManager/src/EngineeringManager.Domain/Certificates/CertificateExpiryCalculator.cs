using EngineeringManager.Domain.Reminders;

namespace EngineeringManager.Domain.Certificates;

public enum CertificateExpiryState
{
    LongTerm = 1,
    Normal = 2,
    Info = 3,
    Warning = 4,
    Critical = 5,
    Expired = 6
}

public static class CertificateExpiryCalculator
{
    public static ReminderSeverity? GetReminderSeverity(DateOnly today, DateOnly? expiresOn)
    {
        if (!expiresOn.HasValue || expiresOn > today.AddMonths(3)) return null;
        if (expiresOn > today.AddMonths(2)) return ReminderSeverity.Info;
        if (expiresOn > today.AddMonths(1)) return ReminderSeverity.Warning;
        return ReminderSeverity.Critical;
    }

    public static CertificateExpiryState GetState(DateOnly today, DateOnly? expiresOn)
    {
        if (!expiresOn.HasValue) return CertificateExpiryState.LongTerm;
        if (expiresOn < today) return CertificateExpiryState.Expired;
        return GetReminderSeverity(today, expiresOn) switch
        {
            ReminderSeverity.Info => CertificateExpiryState.Info,
            ReminderSeverity.Warning => CertificateExpiryState.Warning,
            ReminderSeverity.Critical => CertificateExpiryState.Critical,
            _ => CertificateExpiryState.Normal
        };
    }
}
