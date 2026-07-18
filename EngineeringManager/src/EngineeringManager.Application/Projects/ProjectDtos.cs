using EngineeringManager.Domain.Finance;
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
    IReadOnlyCollection<Guid> LegalEntityIds,
    string? ParentProjectName = null,
    string? GeneralContractorContact = null,
    string? GeneralContractorPhone = null,
    ProjectAffiliationType AffiliationType = ProjectAffiliationType.SelfOperated,
    DateOnly? ActualStartDate = null,
    DateOnly? ActualCompletionDate = null,
    string? Notes = null,
    ContractSigningStatus ContractSigningStatus = ContractSigningStatus.NotSigned,
    IReadOnlyCollection<ProjectTaxConfigurationInput>? TaxConfigurations = null);

public sealed record ProjectTaxConfigurationInput(decimal TaxRate, ProjectInvoiceType InvoiceType);

public sealed record ProjectTaxConfigurationDto(
    Guid Id,
    decimal TaxRate,
    ProjectInvoiceType InvoiceType,
    bool IsActive,
    Guid ConcurrencyStamp);

public sealed record ContractAllocationRequest(Guid LegalEntityId, decimal? Amount, decimal? Percentage);

public sealed record CreateContractRequest(
    Guid ProjectId,
    string ContractNumber,
    string Name,
    ContractType ContractType,
    ContractAllocationMode AllocationMode,
    string? CounterpartyName,
    decimal TotalAmount,
    IReadOnlyCollection<ContractAllocationRequest> Allocations,
    string? Notes = null);

public sealed record CreateContractLineItemRequest(
    Guid ContractId,
    string Code,
    string Name,
    string Unit,
    decimal? EstimatedQuantity,
    decimal? EstimatedUnitPrice,
    decimal? SettledQuantity,
    decimal? SettledUnitPrice,
    bool IsSettlementConfirmed,
    string? Notes = null);

public sealed record UpdateContractLineItemRequest(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    decimal? EstimatedQuantity,
    decimal? EstimatedUnitPrice,
    decimal? SettledQuantity,
    decimal? SettledUnitPrice,
    bool IsSettlementConfirmed,
    Guid ConcurrencyStamp,
    string? Notes = null,
    string? UserId = null,
    string? Reason = null);

public sealed record ProjectDto(
    Guid Id,
    string ProjectNumber,
    string Name,
    string? GeneralContractorName,
    ProjectStage Stage,
    ProjectAffiliationType AffiliationType = ProjectAffiliationType.SelfOperated,
    DateOnly? ActualStartDate = null,
    DateOnly? ActualCompletionDate = null,
    string? Notes = null,
    ContractSigningStatus ContractSigningStatus = ContractSigningStatus.NotSigned,
    IReadOnlyList<ProjectTaxConfigurationDto>? TaxConfigurations = null);

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
    bool IsSettlementConfirmed,
    Guid ConcurrencyStamp,
    string? Notes = null);

public sealed record ContractDto(
    Guid Id,
    string ContractNumber,
    string Name,
    ContractType ContractType,
    ContractAllocationMode AllocationMode,
    decimal TotalAmount,
    IReadOnlyList<ContractLineItemDto> LineItems,
    string? Notes = null);

public sealed record ProjectListItemDto(ProjectDto Project, ProjectSummaryDto Summary);

public sealed record ProjectListActor(string UserId, bool CanAccessAllProjects);

public sealed record ProjectListQuery(
    string? Search,
    IReadOnlyCollection<ProjectStage> Stages,
    Guid? LegalEntityId,
    string? ResponsibleUserId,
    decimal? MinimumCurrentAmount,
    decimal? MaximumCurrentAmount,
    string? SortKey,
    bool SortDescending,
    int Page = 1,
    int PageSize = 20,
    ProjectAffiliationType? AffiliationType = null);

public sealed record ProjectListPageDto(
    IReadOnlyList<ProjectListItemDto> Items,
    ProjectListAggregateDto Aggregate,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<Guid> MatchingProjectIds);

public sealed record ProjectListAggregateDto(
    int ProjectCount,
    decimal ContractAmount,
    decimal CurrentAmount,
    int SettledProjectCount);

public sealed record ProjectFilterOptionDto(string Value, string Label);

public sealed record ProjectListOptionsDto(
    IReadOnlyList<ProjectFilterOptionDto> LegalEntities,
    IReadOnlyList<ProjectFilterOptionDto> ResponsibleUsers);

public sealed record ProjectDetailsDto(
    ProjectDto Project,
    ProjectSummaryDto Summary,
    IReadOnlyList<ContractDto> Contracts);
