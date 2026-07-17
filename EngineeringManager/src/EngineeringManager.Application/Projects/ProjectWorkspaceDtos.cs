using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
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
    ArchiveStatus ArchiveStatus,
    IReadOnlyList<ProjectWorkspaceOptionDto> LegalEntities,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyStamp);

public sealed record ProjectEditOptionsDto(
    IReadOnlyList<ProjectWorkspaceOptionDto> ResponsibleUsers,
    IReadOnlyList<ProjectWorkspaceOptionDto> Departments,
    IReadOnlyList<ProjectWorkspaceOptionDto> Branches,
    IReadOnlyList<ProjectWorkspaceOptionDto> LegalEntities);

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
    ArchiveStatus ArchiveStatus,
    IReadOnlyCollection<Guid> LegalEntityIds,
    Guid ConcurrencyStamp,
    string Reason);

public sealed record ProjectReceivableItemDto(
    Guid Id,
    DateOnly EntryDate,
    DateOnly? DueDate,
    string? ContractNumber,
    string LegalEntityName,
    string? BusinessPartnerName,
    decimal Amount,
    string? Description,
    bool IsVoided);

public sealed record ProjectCollectionItemDto(
    Guid Id,
    DateOnly CollectionDate,
    string? ContractNumber,
    string LegalEntityName,
    string? BusinessPartnerName,
    string AccountName,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Notes);

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
    InvoiceStatus Status);

public sealed record ProjectPayableItemDto(
    Guid Id,
    DateOnly EntryDate,
    DateOnly? DueDate,
    string? ContractNumber,
    string LegalEntityName,
    string BusinessPartnerName,
    decimal Amount,
    string? Description,
    bool IsVoided);

public sealed record ProjectPaymentItemDto(
    Guid Id,
    DateOnly PaymentDate,
    string? ContractNumber,
    string LegalEntityName,
    string BusinessPartnerName,
    string AccountName,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Notes);

public sealed record ProjectActivityItemDto(
    DateTimeOffset OccurredAt,
    string Category,
    string Title,
    string? Detail,
    string? UserName,
    string Tone);

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
    IReadOnlyList<ProjectActivityItemDto> Activities);
