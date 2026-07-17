using System.Security.Claims;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Admin.Settings;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class IndexModel(ISystemSettingsService settingsService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsReadOnly => !User.IsInRole(SystemRoles.SystemAdministrator);

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken token)
    {
        Input = InputModel.From(await settingsService.GetAsync(token));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        if (IsReadOnly) return Forbid();
        if (!ModelState.IsValid) return Page();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("当前用户缺少标识。");
        var userName = User.Identity?.Name ?? "系统管理员";
        await settingsService.SaveAsync(new SettingsActor(userId, userName, true), Input.ToSettings(), token);
        StatusMessage = "显示与交互设置已保存并全站生效。";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        public VisualTheme Theme { get; set; } = VisualTheme.Default;
        public MotionStyle Motion { get; set; } = MotionStyle.Technology;
        public UiEffectsLevel Effects { get; set; } = UiEffectsLevel.Medium;
        public GlobalFont Font { get; set; } = GlobalFont.SystemDefault;
        public TableDensity Density { get; set; } = TableDensity.Standard;

        public SystemDisplaySettings ToSettings() => new(Theme, Motion, Effects, Font, Density);
        public static InputModel From(SystemDisplaySettings settings) => new()
        {
            Theme = settings.Theme,
            Motion = settings.Motion,
            Effects = settings.Effects,
            Font = settings.Font,
            Density = settings.Density
        };
    }
}
