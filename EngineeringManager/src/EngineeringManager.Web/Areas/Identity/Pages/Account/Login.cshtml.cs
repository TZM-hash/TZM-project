using System.ComponentModel.DataAnnotations;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; private set; } = "/";

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Content("~/") : returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Content("~/") : returnUrl;
        if (!ModelState.IsValid) return Page();
        var user = await userManager.FindByEmailAsync(Input.Email) ?? await userManager.FindByNameAsync(Input.Email);
        if (user is null || !user.IsEnabled)
        {
            ModelState.AddModelError(string.Empty, "账号或密码错误，或者账号已停用。");
            return Page();
        }
        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(ReturnUrl);
        if (result.IsLockedOut) ModelState.AddModelError(string.Empty, "账号已暂时锁定，请稍后重试或联系管理员。");
        else ModelState.AddModelError(string.Empty, "账号或密码错误。");
        return Page();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "请输入账号或邮箱。")]
        [Display(Name = "账号或邮箱")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入密码。")]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "保持登录")]
        public bool RememberMe { get; set; }
    }
}
