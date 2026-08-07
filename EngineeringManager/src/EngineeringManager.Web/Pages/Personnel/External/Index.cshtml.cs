using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Pages.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Personnel.External;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel : PersonnelWorkspacePageModel
{
    public IndexModel(
        IPersonnelService personnelService,
        IBusinessYearService? businessYearService = null,
        IEmployeeAnnualLedgerService? annualLedgerService = null,
        ISavedDataViewService? savedViewService = null,
        IPersonnelWorkbookService? personnelWorkbookService = null)
        : base(
            personnelService,
            businessYearService,
            annualLedgerService,
            savedViewService,
            personnelWorkbookService,
            PersonnelScope.External,
            "personnel-external",
            "personnel-external-table",
            "外部人员")
    {
    }

    public IReadOnlyDictionary<Guid, EmployeeAnnualLedgerSummary> SalarySummaries => AnnualSummaries;

    public Task<IActionResult> OnPostSaveViewAsync(CancellationToken cancellationToken) => SaveViewAsync(cancellationToken);

    public Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken) => ExportAsync(cancellationToken);

    protected override PersonnelListQuery BuildQuery() => new(
        PersonnelScope.External,
        Search,
        null,
        BusinessPartnerId,
        DepartmentId,
        null,
        ExternalType,
        IsActive,
        AsOf,
        CrewBusinessPartnerId);

    protected override string PersonnelTypeLabel(PersonnelListItemDto item) => item.ExternalType switch
    {
        ExternalPersonnelType.ConstructionCrew => "施工班组人员",
        ExternalPersonnelType.BusinessPartner => "合作单位人员",
        ExternalPersonnelType.Other => "其他外部人员",
        _ => "外部人员"
    };
}
