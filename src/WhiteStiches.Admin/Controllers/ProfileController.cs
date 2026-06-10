using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Current staff member's own security profile: TOTP two-factor auth (AD-SET-02).</summary>
[Route("profile")]
public class ProfileController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditService audit) : Controller
{
    private const string Issuer = "White Stitches Admin";

    [HttpGet("2fa")]
    public async Task<IActionResult> TwoFactor()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Auth");

        ViewData["Title"] = "Two-factor authentication";
        ViewData["Nav"] = "profile";

        return View(new TwoFactorStatusViewModel
        {
            Enabled = user.TwoFactorEnabled,
            RecoveryCodesLeft = user.TwoFactorEnabled ? await userManager.CountRecoveryCodesAsync(user) : 0
        });
    }

    /// <summary>Resets the authenticator key, then PRG to the setup screen that displays it.</summary>
    [HttpPost("2fa/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Auth");

        await userManager.ResetAuthenticatorKeyAsync(user);

        await audit.LogAsync("staff.2fa.start", user.Id, user.Email,
            entityType: "StaffUser", entityId: user.Id.ToString(),
            ipAddress: ClientIp(), ct: ct);

        return RedirectToAction(nameof(Setup));
    }

    [HttpGet("2fa/setup")]
    public async Task<IActionResult> Setup()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Auth");

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            TempData["Err"] = "Start two-factor setup first.";
            return RedirectToAction(nameof(TwoFactor));
        }

        ViewData["Title"] = "Set up authenticator";
        ViewData["Nav"] = "profile";

        return View(new TwoFactorSetupViewModel
        {
            SharedKey = FormatKey(key),
            OtpAuthUri = BuildOtpAuthUri(user.Email ?? user.UserName ?? "staff", key)
        });
    }

    [HttpPost("2fa/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(string? code, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Auth");

        var sanitized = Sanitize(code);
        if (sanitized.Length == 0)
        {
            TempData["Err"] = "Enter the 6-digit code from your authenticator app.";
            return RedirectToAction(nameof(Setup));
        }

        var valid = await userManager.VerifyTwoFactorTokenAsync(user,
            userManager.Options.Tokens.AuthenticatorTokenProvider, sanitized);

        if (!valid)
        {
            TempData["Err"] = "That code didn't match. Check the key in your app and try again.";
            return RedirectToAction(nameof(Setup));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await signInManager.RefreshSignInAsync(user);

        await audit.LogAsync("staff.2fa.enable", user.Id, user.Email,
            entityType: "StaffUser", entityId: user.Id.ToString(),
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = "Two-factor authentication is enabled.";
        TempData["RecoveryCodes"] = string.Join("\n", codes ?? []);
        return RedirectToAction(nameof(RecoveryCodes));
    }

    /// <summary>Shows the freshly generated recovery codes exactly once (TempData semantics).</summary>
    [HttpGet("2fa/recovery-codes")]
    public IActionResult RecoveryCodes()
    {
        if (TempData["RecoveryCodes"] is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return RedirectToAction(nameof(TwoFactor));
        }

        ViewData["Title"] = "Recovery codes";
        ViewData["Nav"] = "profile";

        return View(new RecoveryCodesViewModel
        {
            Codes = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        });
    }

    [HttpPost("2fa/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(string? code, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Auth");

        if (!user.TwoFactorEnabled)
        {
            TempData["Err"] = "Two-factor authentication is not enabled.";
            return RedirectToAction(nameof(TwoFactor));
        }

        var sanitized = Sanitize(code);
        var valid = sanitized.Length > 0 && await userManager.VerifyTwoFactorTokenAsync(user,
            userManager.Options.Tokens.AuthenticatorTokenProvider, sanitized);

        if (!valid)
        {
            TempData["Err"] = "A valid authenticator code is required to disable two-factor authentication.";
            return RedirectToAction(nameof(TwoFactor));
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await signInManager.RefreshSignInAsync(user);

        await audit.LogAsync("staff.2fa.disable", user.Id, user.Email,
            entityType: "StaffUser", entityId: user.Id.ToString(),
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = "Two-factor authentication is disabled.";
        return RedirectToAction(nameof(TwoFactor));
    }

    // ------------------------------------------------------------------ helpers

    private static string Sanitize(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

    /// <summary>Formats the Base32 key in groups of four for manual entry.</summary>
    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(key, i, Math.Min(4, key.Length - i));
        }

        return sb.ToString().ToLowerInvariant();
    }

    private static string BuildOtpAuthUri(string account, string key) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
