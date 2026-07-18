using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects.Contracts;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager)]
public sealed class EditModel(IProjectService projectService, IProjectWorkspaceService workspaceService) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public Guid? ProjectId { get; set; }
    [BindProperty] public string ContractNumber { get; set; } = string.Empty;
    [BindProperty] public string ContractName { get; set; } = string.Empty;
    [BindProperty] public ContractType ContractType { get; set; } = ContractType.MainContract;
    [BindProperty] public string? CounterpartyName { get; set; }
    [BindProperty] public decimal ContractAmount { get; set; }
    [BindProperty] public Guid? ContractLegalEntityId { get; set; }
    [BindProperty] public Guid? ContractId { get; set; }
    [BindProperty] public string LineCode { get; set; } = string.Empty;
    [BindProperty] public string LineName { get; set; } = string.Empty;
    [BindProperty] public string Unit { get; set; } = string.Empty;
    [BindProperty] public decimal? EstimatedQuantity { get; set; }
    [BindProperty] public decimal? EstimatedUnitPrice { get; set; }
    [BindProperty] public decimal? SettledQuantity { get; set; }
    [BindProperty] public decimal? SettledUnitPrice { get; set; }
    [BindProperty] public bool IsSettlementConfirmed { get; set; }
    [BindProperty] public string? ContractNotes { get; set; }
    [BindProperty] public string? LineNotes { get; set; }

    public async Task OnGetAsync(CancellationToken token) => await LoadAsync(token);

    public async Task<IActionResult> OnPostContractAsync(CancellationToken token)
    {
        if (!ProjectId.HasValue) return RedirectToPage();
        try
        {
            var legalEntityId = ContractLegalEntityId ?? throw new ArgumentException("请选择我方签约公司。");
            await projectService.AddContractAsync(new CreateContractRequest(ProjectId.Value, ContractNumber, ContractName, ContractType,
                ContractAllocationMode.SingleCompany, CounterpartyName, ContractAmount,
                [new ContractAllocationRequest(legalEntityId, ContractAmount, null)], ContractNotes), token);
            return RedirectToPage(new { projectId = ProjectId.Value });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLineAsync(CancellationToken token)
    {
        if (!ProjectId.HasValue || !ContractId.HasValue) return RedirectToPage(new { projectId = ProjectId });
        try
        {
            await projectService.AddLineItemAsync(new CreateContractLineItemRequest(ContractId.Value, LineCode, LineName, Unit,
                EstimatedQuantity, EstimatedUnitPrice, SettledQuantity, SettledUnitPrice, IsSettlementConfirmed, LineNotes), token);
            return RedirectToPage(new { projectId = ProjectId.Value });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(token);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken token)
    {
        Projects = await projectService.ListProjectsAsync(null, null, token);
        Workspace = ProjectId.HasValue ? await workspaceService.GetAsync(ProjectId.Value, token) : null;
        if (Workspace is not null && !ContractLegalEntityId.HasValue)
            ContractLegalEntityId = Workspace.Overview.LegalEntities.Select(item => Guid.TryParse(item.Value, out var id) ? id : Guid.Empty).FirstOrDefault(item => item != Guid.Empty);
    }
}
