using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public sealed record ProjectSummaryDto(
    decimal ContractAmount,
    decimal EstimatedAmount,
    decimal SettledAmount,
    decimal CurrentAmount,
    ProjectSettlementStatus SettlementStatus,
    int ContractCount,
    int LineItemCount,
    decimal InvoiceRequiredAmount = 0m);
