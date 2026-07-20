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
        var amountSummary = ProjectAmountCalculator.Calculate(project.Stage, lineItems.Select(line =>
            new LineItemAmountInput(line.Quantity, line.UnitPrice, line.RequiresInvoice)));
        return new ProjectSummaryDto(
            contracts.Sum(contract => contract.TotalAmount),
            amountSummary.EstimatedAmount,
            amountSummary.SettledAmount,
            amountSummary.CurrentAmount,
            amountSummary.SettlementStatus,
            contracts.Length,
            lineItems.Length,
            amountSummary.InvoiceRequiredAmount);
    }
}
