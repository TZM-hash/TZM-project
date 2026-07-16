using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public sealed record CreateProjectRequest(
    string ProjectNumber,
    string Name,
    string? GeneralContractorName,
    string? ResponsibleUserId,
    Guid? DepartmentId,
    Guid? BranchId,
    ProjectStage Stage,
    ArchiveStatus ArchiveStatus,
    IReadOnlyCollection<Guid> LegalEntityIds);

public sealed record ContractAllocationRequest(Guid LegalEntityId, decimal? Amount, decimal? Percentage);

public sealed record CreateContractRequest(
    Guid ProjectId,
    string ContractNumber,
    string Name,
    ContractType ContractType,
    ContractAllocationMode AllocationMode,
    string? CounterpartyName,
    decimal TotalAmount,
    IReadOnlyCollection<ContractAllocationRequest> Allocations);

public sealed record CreateContractLineItemRequest(
    Guid ContractId,
    string Code,
    string Name,
    string Unit,
    decimal? EstimatedQuantity,
    decimal? EstimatedUnitPrice,
    decimal? SettledQuantity,
    decimal? SettledUnitPrice,
    bool IsSettlementConfirmed);

public sealed record ProjectDto(
    Guid Id,
    string ProjectNumber,
    string Name,
    string? GeneralContractorName,
    ProjectStage Stage,
    ArchiveStatus ArchiveStatus);

public sealed record ContractLineItemDto(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    decimal? EstimatedQuantity,
    decimal? EstimatedUnitPrice,
    decimal EstimatedAmount,
    decimal? SettledQuantity,
    decimal? SettledUnitPrice,
    decimal SettledAmount,
    bool IsSettlementConfirmed);

public sealed record ContractDto(
    Guid Id,
    string ContractNumber,
    string Name,
    ContractType ContractType,
    ContractAllocationMode AllocationMode,
    decimal TotalAmount,
    IReadOnlyList<ContractLineItemDto> LineItems);

public sealed record ProjectListItemDto(ProjectDto Project, ProjectSummaryDto Summary);

public sealed record ProjectDetailsDto(
    ProjectDto Project,
    ProjectSummaryDto Summary,
    IReadOnlyList<ContractDto> Contracts);
