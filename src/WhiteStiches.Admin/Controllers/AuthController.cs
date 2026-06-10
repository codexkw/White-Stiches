using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditService audit) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
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
                    return await CompleteLoginAsync(user, model.ReturnUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction(nameof(LoginTwoFactor),
                        new { returnUrl = model.ReturnUrl, rememberMe = model.RememberMe });
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

    /// <summary>Second step after PasswordSignInAsync returns RequiresTwoFactor.</summary>
    [AllowAnonymous]
    [HttpGet("login/2fa")]
    public async Task<IActionResult> LoginTwoFactor(string? returnUrl = null, bool rememberMe = false)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new LoginTwoFactorViewModel { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    [AllowAnonymous]
    [HttpPost("login/2fa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginTwoFactor(LoginTwoFactorViewModel model)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, model.RememberMachine);

        if (result.Succeeded)
        {
            return await CompleteLoginAsync(user, model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked after repeated failures. Try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
        return View(model);
    }

    /// <summary>Fallback when the authenticator device is unavailable: one-time recovery code.</summary>
    [AllowAnonymous]
    [HttpPost("login/2fa/recovery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginRecovery(string? recoveryCode, string? returnUrl = null, bool rememberMe = false)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var code = (recoveryCode ?? string.Empty).Replace(" ", string.Empty).Trim();
        if (code.Length > 0)
        {
            var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(code);
            if (result.Succeeded)
            {
                return await CompleteLoginAsync(user, returnUrl);
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid recovery code.");
        return View(nameof(LoginTwoFactor), new LoginTwoFactorViewModel { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Shared success path: stamp last login, audit "staff.login", honor returnUrl.</summary>
    private async Task<IActionResult> CompleteLoginAsync(ApplicationUser user, string? returnUrl)
    {
        user.LastLoginAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        await audit.LogAsync("staff.login", user.Id, user.Email,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }
}
