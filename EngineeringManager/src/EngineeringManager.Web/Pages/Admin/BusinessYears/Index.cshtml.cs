using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Admin.BusinessYears;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class IndexModel(IBusinessYearService businessYearService) : PageModel
{
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];

    [BindProperty]
    public BusinessYearInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        BusinessYears = await businessYearService.ListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await businessYearService.CreateAsync(
                new CreateBusinessYearRequest(Input.Name, Input.StartDate, Input.EndDate),
                cancellationToken);
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            BusinessYears = await businessYearService.ListAsync(cancellationToken);
            return Page();
        }
    }

    public sealed class BusinessYearInput
    {
        public string Name { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}
