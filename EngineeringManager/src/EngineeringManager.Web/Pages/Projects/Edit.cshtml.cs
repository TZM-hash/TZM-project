using System.Security.Claims;
using System.Globalization;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager)]
public sealed class EditModel(IProjectService projectService, IProjectWorkspaceService workspaceService) : PageModel
{
    public ProjectEditOptionsDto Options { get; private set; } = new([], [], [], []);
    public bool IsEditing => Id.HasValue;

    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public string ProjectNumber { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? ParentProjectName { get; set; }
    [BindProperty] public string? GeneralContractorName { get; set; }
    [BindProperty] public string? GeneralContractorContact { get; set; }
    [BindProperty] public string? GeneralContractorPhone { get; set; }
    [BindProperty] public string? ResponsibleUserId { get; set; }
    [BindProperty] public Guid? DepartmentId { get; set; }
    [BindProperty] public Guid? BranchId { get; set; }
    [BindProperty] public ProjectStage Stage { get; set; } = ProjectStage.AwaitingMobilization;
    [BindProperty] public ContractSigningStatus ContractSigningStatus { get; set; } = ContractSigningStatus.NotSigned;
    [BindProperty] public ProjectAffiliationType AffiliationType { get; set; } = ProjectAffiliationType.SelfOperated;
    [BindProperty] public DateOnly? ActualStartDate { get; set; }
    [BindProperty] public DateOnly? ActualCompletionDate { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public List<Guid> LegalEntityIds { get; set; } = [];
    [BindProperty] public List<string> TaxConfigurationSelections { get; set; } = [];
    [BindProperty] public Guid ConcurrencyStamp { get; set; }
    [BindProperty] public string Reason { get; set; } = "维护项目资料";

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? copyFrom, CancellationToken token)
    {
        Options = await workspaceService.GetEditOptionsAsync(token);
        var sourceId = id ?? copyFrom;
        if (!sourceId.HasValue) return Page();
        var workspace = await workspaceService.GetAsync(sourceId.Value, token);
        if (workspace is null) return NotFound();
        Populate(workspace.Overview);
        if (copyFrom.HasValue)
        {
            Id = null;
            ProjectNumber = ProjectNumber + "-COPY";
            Name = Name + "（复制）";
            ConcurrencyStamp = Guid.Empty;
            Reason = "复制项目档案";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        try
        {
            if (!Id.HasValue)
            {
                var created = await projectService.CreateProjectAsync(new CreateProjectRequest(
                    ProjectNumber, Name, GeneralContractorName, ResponsibleUserId, DepartmentId, BranchId, Stage,
                    LegalEntityIds, ParentProjectName, GeneralContractorContact, GeneralContractorPhone, AffiliationType,
                    ActualStartDate, ActualCompletionDate, Notes, ContractSigningStatus, ParseTaxConfigurations()), token);
                return RedirectToPage("Details", new { id = created.Id });
            }
            await workspaceService.UpdateAsync(
                new ProjectWorkspaceActor(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", User.Identity?.Name),
                new UpdateProjectRequest(Id.Value, ProjectNumber, Name, ParentProjectName, GeneralContractorName,
                    GeneralContractorContact, GeneralContractorPhone, ResponsibleUserId, DepartmentId, BranchId, Stage, AffiliationType,
                    LegalEntityIds, ConcurrencyStamp, Reason, ActualStartDate, ActualCompletionDate, Notes,
                    ContractSigningStatus, ParseTaxConfigurations()), token);
            return RedirectToPage("Details", new { id = Id.Value });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Options = await workspaceService.GetEditOptionsAsync(token);
            return Page();
        }
    }

    private void Populate(ProjectWorkspaceOverviewDto item)
    {
        Id = item.Id;
        ProjectNumber = item.ProjectNumber;
        Name = item.Name;
        ParentProjectName = item.ParentProjectName;
        GeneralContractorName = item.GeneralContractorName;
        GeneralContractorContact = item.GeneralContractorContact;
        GeneralContractorPhone = item.GeneralContractorPhone;
        ResponsibleUserId = item.ResponsibleUserId;
        DepartmentId = item.DepartmentId;
        BranchId = item.BranchId;
        Stage = item.Stage;
        ContractSigningStatus = item.ContractSigningStatus;
        AffiliationType = item.AffiliationType;
        ActualStartDate = item.ActualStartDate;
        ActualCompletionDate = item.ActualCompletionDate;
        Notes = item.Notes;
        LegalEntityIds = item.LegalEntities.Select(option => Guid.Parse(option.Value)).ToList();
        TaxConfigurationSelections = item.TaxConfigurations?.Where(configuration => configuration.IsActive)
            .Select(configuration => $"{configuration.TaxRate * 100m:0}|{(int)configuration.InvoiceType}").ToList() ?? [];
        ConcurrencyStamp = item.ConcurrencyStamp;
    }

    private ProjectTaxConfigurationInput[] ParseTaxConfigurations() =>
        TaxConfigurationSelections.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item =>
        {
            var parts = item.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var percent) ||
                !Enum.TryParse<EngineeringManager.Domain.Finance.ProjectInvoiceType>(parts[1], out var invoiceType))
                throw new ArgumentException("项目税金配置格式无效。");
            return new ProjectTaxConfigurationInput(percent / 100m, invoiceType);
        }).ToArray();
}
