using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;

namespace EngineeringManager.Infrastructure.Projects;

public static class ProjectSummaryService
{
    public static ProjectSummaryDto Calculate(Project project)
    {
        var contracts = project.Contracts.Where(contract => contract.IsActive).ToArray();
        var lineItems = contracts.SelectMany(contract => contract.LineItems).ToArray();
        var amountSummary = ProjectAmountCalculator.Calculate(lineItems.Select(line =>
            new LineItemAmountInput(
                line.EstimatedQuantity,
                line.EstimatedUnitPrice,
                line.IsSettlementConfirmed ? line.SettledQuantity : null,
                line.IsSettlementConfirmed ? line.SettledUnitPrice : null)));
        return new ProjectSummaryDto(
            contracts.Sum(contract => contract.TotalAmount),
            amountSummary.EstimatedAmount,
            amountSummary.SettledAmount,
            amountSummary.CurrentAmount,
            amountSummary.SettlementStatus,
            contracts.Length,
            lineItems.Length);
    }
}
