using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Personnel.External;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IPersonnelService personnelService) : PageModel
{
    public IReadOnlyList<PersonnelListItemDto> Personnel { get; private set; } = [];
    public PersonnelOptionSetDto Options { get; private set; } = new([], [], [], [], []);
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.Finance);

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CrewBusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public ExternalPersonnelType? ExternalType { get; set; }
    [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? AsOf { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Options = await personnelService.GetOptionsAsync(cancellationToken);
        Personnel = await personnelService.ListAsync(
            new PersonnelListQuery(
                PersonnelScope.External,
                Search,
                null,
                BusinessPartnerId,
                DepartmentId,
                null,
                ExternalType,
                IsActive,
                AsOf,
                CrewBusinessPartnerId),
            CanViewSensitive,
            cancellationToken);
    }
}
