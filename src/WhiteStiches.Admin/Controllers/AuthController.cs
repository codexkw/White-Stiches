using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

[AllowAnonymous]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditService audit) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Intersect(AppRoles.StaffRoles).Any())
            {
                var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    user.LastLoginAtUtc = DateTime.UtcNow;
                    await userManager.UpdateAsync(user);

                    await audit.LogAsync("staff.login", user.Id, user.Email,
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Dashboard");
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Account locked after repeated failures. Try again in 15 minutes.");
                    return View(model);
                }
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid credentials or no staff access.");
        return View(model);
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
