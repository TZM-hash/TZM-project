using EngineeringManager.Application.StageResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.StageResults;

[Authorize]
public sealed class IndexModel : PageModel
{
    public IReadOnlyList<StageResultDto> Results { get; } = [];

    public IActionResult OnGet() => RedirectToPage("/Projects/Index");
}
