using EngineeringManager.Application.StageResults;
using EngineeringManager.Domain.Security;
using EngineeringManager.Domain.StageResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.StageResults;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff)]
public sealed class CreateModel(IStageResultService stageResultService) : PageModel
{
    [BindProperty] public Guid ProjectId { get; set; }
    [BindProperty] public Guid? ContractId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public DateOnly ResultDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public StageResultType ResultType { get; set; } = StageResultType.Progress;
    [BindProperty] public StageResultStatus Status { get; set; } = StageResultStatus.Draft;
    [BindProperty] public QualityResult QualityResult { get; set; } = QualityResult.NotChecked;
    [BindProperty] public string? Description { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await stageResultService.CreateAsync(
            new CreateStageResultRequest(
                ProjectId,
                ContractId,
                Title,
                ResultType,
                Status,
                ResultDate,
                Description,
                QualityResult,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                Status == StageResultStatus.Draft,
                [],
                []),
            cancellationToken);
        return RedirectToPage("/StageResults/Index");
    }
}
