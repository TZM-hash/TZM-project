using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Reminders;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class CertificateExpiryCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 17);

    [Theory]
    [InlineData(2026, 10, 18, null)]
    [InlineData(2026, 10, 17, ReminderSeverity.Info)]
    [InlineData(2026, 9, 18, ReminderSeverity.Info)]
    [InlineData(2026, 9, 17, ReminderSeverity.Warning)]
    [InlineData(2026, 8, 18, ReminderSeverity.Warning)]
    [InlineData(2026, 8, 17, ReminderSeverity.Critical)]
    [InlineData(2026, 7, 16, ReminderSeverity.Critical)]
    public void UsesNaturalMonthBoundaries(int year, int month, int day, ReminderSeverity? expected)
    {
        CertificateExpiryCalculator.GetReminderSeverity(Today, new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Fact]
    public void LongTermCertificateHasNoReminder()
    {
        CertificateExpiryCalculator.GetReminderSeverity(Today, null).Should().BeNull();
    }
}
