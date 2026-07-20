using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Ledger.Years;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class IndexModel(IFinanceBusinessYearService service, ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<FinanceBusinessYearDto> Items { get; private set; } = [];
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public DateOnly StartDate { get; set; } = new(DateTime.Today.Year, 1, 1);
    [BindProperty] public DateOnly EndDate { get; set; } = new(DateTime.Today.Year, 12, 31);
    [BindProperty] public Guid DeleteId { get; set; }
    [BindProperty] public Guid DeleteConcurrencyStamp { get; set; }
    [BindProperty] public string DeleteReason { get; set; } = string.Empty;

    public IActionResult OnGet() => RedirectToPage("/Admin/FinanceYears/Index");

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            await service.CreateAsync(actor, new CreateFinanceBusinessYearRequest(Name, StartDate, EndDate), token);
            TempData["Success"] = "财务业务年度已创建。";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            await service.DeleteAsync(actor, DeleteId, DeleteConcurrencyStamp, DeleteReason, token);
            TempData["Success"] = "财务业务年度已删除。";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToPage();
    }
}
