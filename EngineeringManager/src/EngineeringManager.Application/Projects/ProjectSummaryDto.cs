using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public sealed record ProjectSummaryDto(
    decimal ContractAmount,
    decimal CurrentAmount,
    ProjectSettlementStatus SettlementStatus,
    int ContractCount,
    decimal InvoiceRequiredAmount = 0m);
