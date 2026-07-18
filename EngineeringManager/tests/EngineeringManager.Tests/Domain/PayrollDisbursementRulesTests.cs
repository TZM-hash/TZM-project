using EngineeringManager.Domain.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class PayrollDisbursementRulesTests
{
    [Fact]
    public void MixedRecipientsProduceCategoryAndCrewTotals()
    {
        var crewId = Guid.NewGuid();
        var summary = PayrollDisbursementRules.Calculate(
            10_000m,
            [
                PayrollDisbursementLineInput.ForEmployee(Guid.NewGuid(), 3_000m),
                PayrollDisbursementLineInput.ForCrewWorker(Guid.NewGuid(), crewId, 4_000m),
                PayrollDisbursementLineInput.ForTemporaryWorker(Guid.NewGuid(), 3_000m)
            ]);

        summary.EmployeeAmount.Should().Be(3_000m);
        summary.CrewAmount.Should().Be(4_000m);
        summary.TemporaryAmount.Should().Be(3_000m);
        summary.DetailAmount.Should().Be(10_000m);
        summary.Difference.Should().Be(0m);
        summary.CrewAmounts.Should().ContainSingle().Which.Should().Be(new PayrollCrewAmount(crewId, 4_000m));
    }

    [Fact]
    public void ReviewedBatchRequiresExactTotalAndProjectForCrewWorkers()
    {
        var crewId = Guid.NewGuid();
        var lines = new[]
        {
            PayrollDisbursementLineInput.ForCrewWorker(Guid.NewGuid(), crewId, 4_000m)
        };

        var missingProject = () => PayrollDisbursementRules.EnsureCanReview(4_000m, null, lines);
        var mismatchedTotal = () => PayrollDisbursementRules.EnsureCanReview(5_000m, Guid.NewGuid(), lines);

        missingProject.Should().Throw<InvalidOperationException>().WithMessage("*项目*");
        mismatchedTotal.Should().Throw<InvalidOperationException>().WithMessage("*差额*");
    }

    [Fact]
    public void RecipientReferenceMustMatchTypeAndCannotRepeatInOneBatch()
    {
        var employeeId = Guid.NewGuid();
        var invalidReference = new PayrollDisbursementLineInput(
            PayrollRecipientType.Employee,
            employeeId,
            Guid.NewGuid(),
            null,
            null,
            1_000m);
        var duplicates = new[]
        {
            PayrollDisbursementLineInput.ForEmployee(employeeId, 1_000m),
            PayrollDisbursementLineInput.ForEmployee(employeeId, 500m)
        };

        var invalidAction = () => PayrollDisbursementRules.Calculate(1_000m, [invalidReference]);
        var duplicateAction = () => PayrollDisbursementRules.Calculate(1_500m, duplicates);

        invalidAction.Should().Throw<ArgumentException>().WithMessage("*人员来源*");
        duplicateAction.Should().Throw<InvalidOperationException>().WithMessage("*重复*");
    }
}
