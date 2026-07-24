namespace EngineeringManager.Application.Companies;

public interface ICompanyManagementService
{
    Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyListItemDto>> SearchAsync(CompanyActor actor, string? search, CancellationToken cancellationToken) => ListAsync(actor, cancellationToken);
    Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken);
    Task<CompanyDetailsDto> SaveCompanyAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken cancellationToken);
    Task<SaveCompanyRequest> PrepareCopyAsync(CompanyActor actor, Guid sourceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);
    Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken);
    Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken);
    Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken);
    Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken);

    Task<CompanyWorkspaceSummaryDto> GetWorkspaceSummaryAsync(CompanyActor actor, Guid companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyActivityItemDto>> ListRecentActivityAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyProjectRowDto>> ListCompanyProjectsAsync(CompanyActor actor, Guid companyId, string? search, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyContractRowDto>> ListCompanyContractsAsync(CompanyActor actor, Guid companyId, Guid? projectId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyCollectionRowDto>> ListCompanyCollectionsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyPaymentRowDto>> ListCompanyPaymentsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyInvoiceRowDto>> ListCompanyInvoicesAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken);
}

