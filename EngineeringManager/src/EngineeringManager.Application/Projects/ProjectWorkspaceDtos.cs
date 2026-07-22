using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public sealed record ProjectWorkspaceActor(string UserId, string? UserName);

public sealed record ProjectWorkspaceOptionDto(string Value, string Label);

public sealed record ProjectWorkspaceOverviewDto(
    Guid Id,
    string ProjectNumber,
    string Name,
    string? ParentProjectName,
    string? GeneralContractorName,
    string? GeneralContractorContact,
    string? GeneralContractorPhone,
    string? ResponsibleUserId,
    string? ResponsibleUserName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? BranchId,
    string? BranchName,
    ProjectStage Stage,
    ProjectAffiliationType AffiliationType,
    IReadOnlyList<ProjectWorkspaceOptionDto> LegalEntities,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyStamp,
    DateOnly? ActualStartDate = null,
    DateOnly? ActualCompletionDate = null,
    string? Notes = null,
    ContractSigningStatus ContractSigningStatus = ContractSigningStatus.NotSigned,
    IReadOnlyList<ProjectTaxConfigurationDto>? TaxConfigurations = null);

public sealed record ProjectOverviewEquipmentDto(
    Guid ConstructionRecordId,
    Guid EquipmentId,
    string EquipmentNumber,
    string EquipmentName,
    DateOnly? EntryDate,
    DateOnly? ExitDate);

public sealed record ProjectEditOptionsDto(
    IReadOnlyList<ProjectWorkspaceOptionDto> ResponsibleUsers,
    IReadOnlyList<ProjectWorkspaceOptionDto> Departments,
    IReadOnlyList<ProjectWorkspaceOptionDto> Branches,
    IReadOnlyList<ProjectWorkspaceOptionDto> LegalEntities);

public sealed record ProjectContractQuickEditInput(
    Guid? Id,
    string Name,
    decimal? TotalAmount,
    Guid ConcurrencyStamp);

public sealed record UpdateProjectRequest(
    Guid Id,
    string ProjectNumber,
    string Name,
    string? ParentProjectName,
    string? GeneralContractorName,
    string? GeneralContractorContact,
    string? GeneralContractorPhone,
    string? ResponsibleUserId,
    Guid? DepartmentId,
    Guid? BranchId,
    ProjectStage Stage,
    ProjectAffiliationType AffiliationType,
    IReadOnlyCollection<Guid> LegalEntityIds,
    Guid ConcurrencyStamp,
    string Reason,
    DateOnly? ActualStartDate = null,
    DateOnly? ActualCompletionDate = null,
    string? Notes = null,
    ContractSigningStatus ContractSigningStatus = ContractSigningStatus.NotSigned,
    IReadOnlyCollection<ProjectTaxConfigurationInput>? TaxConfigurations = null,
    IReadOnlyCollection<ProjectContractQuickEditInput>? Contracts = null);

public sealed record ProjectReceivableItemDto(
    Guid Id,
    DateOnly EntryDate,
    DateOnly? DueDate,
    string? ContractNumber,
    string LegalEntityName,
    string? BusinessPartnerName,
    decimal Amount,
    string? Description,
    bool IsVoided,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid ConcurrencyStamp = default);

public sealed record ProjectCollectionItemDto(
    Guid Id,
    DateOnly CollectionDate,
    string? ContractNumber,
    string LegalEntityName,
    string? BusinessPartnerName,
    string AccountName,
    decimal Amount,
    string? PaymentMethod,
    string? Notes,
    Guid? ReceivableEntryId = null,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid? AccountId = null,
    Guid ConcurrencyStamp = default);

public sealed record ProjectInvoiceItemDto(
    Guid Id,
    DateOnly InvoiceDate,
    string InvoiceNumber,
    InvoiceDirection Direction,
    string? ContractNumber,
    string LegalEntityName,
    string? BusinessPartnerName,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    InvoiceStatus Status,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    string? InvoiceType = null,
    Guid? ProjectTaxConfigurationId = null,
    Guid ConcurrencyStamp = default,
    string? Notes = null);

public sealed record ProjectPayableItemDto(
    Guid Id,
    DateOnly EntryDate,
    DateOnly? DueDate,
    string? ContractNumber,
    string LegalEntityName,
    string BusinessPartnerName,
    decimal Amount,
    string? Description,
    bool IsVoided,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid ConcurrencyStamp = default);

public sealed record ProjectPaymentItemDto(
    Guid Id,
    DateOnly PaymentDate,
    string? ContractNumber,
    string LegalEntityName,
    string BusinessPartnerName,
    string AccountName,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Notes,
    Guid? PayableEntryId = null,
    Guid? ContractId = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid? AccountId = null,
    Guid ConcurrencyStamp = default,
    string SourceType = "FinancePayment",
    Guid? PayrollBatchId = null,
    Guid? PayrollPaymentId = null);

public sealed record ProjectActivityItemDto(
    DateTimeOffset OccurredAt,
    string Category,
    string Title,
    string? Detail,
    string? UserName,
    string Tone);

public sealed record ProjectMilestoneDto(
    Guid Id,
    string Name,
    DateOnly? PlannedDate,
    DateOnly? ActualDate,
    bool IsCompleted,
    int SortOrder,
    string? Notes);

public sealed record ProjectAssignmentDto(
    Guid Id,
    string UserId,
    string UserName,
    ProjectAssignmentType AssignmentType,
    bool IsActive,
    string? Notes);

public sealed record ProjectPartnerLinkDto(
    Guid Id,
    Guid PartnerId,
    string PartnerName,
    BusinessPartnerRoleType RoleType,
    Guid? ContractId,
    string? ContractNumber,
    bool IsPrimary,
    bool IsActive,
    string? Notes);

public sealed record ProjectWorkspaceDto(
    ProjectWorkspaceOverviewDto Overview,
    ProjectSummaryDto ProjectSummary,
    FinanceProjectSummaryDto FinanceSummary,
    IReadOnlyList<ContractDto> Contracts,
    IReadOnlyList<ProjectReceivableItemDto> Receivables,
    IReadOnlyList<ProjectCollectionItemDto> Collections,
    IReadOnlyList<ProjectInvoiceItemDto> Invoices,
    IReadOnlyList<ProjectPayableItemDto> Payables,
    IReadOnlyList<ProjectPaymentItemDto> Payments,
    IReadOnlyList<ProjectActivityItemDto> Activities,
    IReadOnlyList<ProjectMilestoneDto>? Milestones = null,
    IReadOnlyList<ProjectAssignmentDto>? Assignments = null,
    IReadOnlyList<ProjectPartnerLinkDto>? Partners = null,
    IReadOnlyList<ProjectOverviewEquipmentDto>? OverviewEquipment = null);
