using System.Security.Claims;
using EngineeringManager.Application.ConstructionCrews;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Crews;

[Authorize]
public sealed class DetailsModel(IConstructionCrewService crewService) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public WorkerInput NewWorker { get; set; } = new();
    [BindProperty] public TransferInput Transfer { get; set; } = new();
    public ConstructionCrewDetailsDto Details { get; private set; } = null!;
    public IReadOnlyList<ConstructionCrewListItemDto> CrewOptions { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAddWorkerAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await crewService.AddWorkerAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", new CreateConstructionWorkerRequest(Id, NewWorker.Name, NewWorker.IdentityNumber, NewWorker.Phone, NewWorker.BankAccountNumber, NewWorker.BankName, NewWorker.Trade, NewWorker.StartDate, NewWorker.Notes, NewWorker.Reason), cancellationToken);
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostTransferWorkerAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await crewService.TransferWorkerAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", new TransferConstructionWorkerRequest(Transfer.WorkerId, Transfer.NewCrewId, Transfer.TransferDate, Transfer.Reason), cancellationToken);
        return RedirectToPage(new { id = Id });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var canViewSensitive = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
        var details = await crewService.GetAsync(Id, canViewSensitive, cancellationToken);
        if (details is null) return false;
        Details = details;
        CrewOptions = await crewService.ListAsync(false, cancellationToken);
        NewWorker.StartDate = DateOnly.FromDateTime(DateTime.Today);
        NewWorker.Reason = "维护班组人员名册";
        Transfer.TransferDate = DateOnly.FromDateTime(DateTime.Today);
        Transfer.Reason = "施工班组人员转组";
        return true;
    }

    public sealed class WorkerInput { public string Name { get; set; } = string.Empty; public string? IdentityNumber { get; set; } public string? Phone { get; set; } public string? BankAccountNumber { get; set; } public string? BankName { get; set; } public string? Trade { get; set; } public DateOnly StartDate { get; set; } public string? Notes { get; set; } public string Reason { get; set; } = string.Empty; }
    public sealed class TransferInput { public Guid WorkerId { get; set; } public Guid NewCrewId { get; set; } public DateOnly TransferDate { get; set; } public string Reason { get; set; } = string.Empty; }
}
