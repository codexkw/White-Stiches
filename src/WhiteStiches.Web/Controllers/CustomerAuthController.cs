using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Web.Infrastructure;
using WhiteStiches.Web.Models.Auth;

namespace WhiteStiches.Web.Controllers;

/// <summary>
/// Customer sign-in / registration / forgot-password / logout.
/// The [Authorize] self-service pages live in AccountController; this controller is anonymous
/// (Identity cookie LoginPath points at /account/login).
/// </summary>
public class CustomerAuthController : Controller
{
    private const string LoginViewPath = "~/Views/Account/Login.cshtml";
    private const string ForgotSentToTempDataKey = "AuthForgotSentTo";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartService _cartService;

    public CustomerAuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ICartService cartService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _cartService = cartService;
    }

    // ── GET /account/login ──────────────────────────────────────────────
    [HttpGet("account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/account");

        var model = new AuthPageViewModel { ReturnUrl = returnUrl };

        // Forgot-password PRG round-trip: show the "check your inbox" state.
        if (TempData[ForgotSentToTempDataKey] is string sentTo)
        {
            model.ActivePane = AuthPageViewModel.PaneForgot;
            model.ForgotSuccess = true;
            model.ForgotSentTo = sentTo;
        }

        return View(LoginViewPath, model);
    }

    // ── POST /account/login ─────────────────────────────────────────────
    [HttpPost("account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [Bind(Prefix = "SignIn")] SignInFormModel form,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return LoginView(AuthPageViewModel.PaneSignIn, returnUrl, signIn: form);

        var user = await _userManager.FindByEmailAsync(form.Email.Trim());
        if (user is not null)
        {
            // Staff may shop too — no role gate here.
            var result = await _signInManager.PasswordSignInAsync(user, form.Password, form.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginAtUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                await MergeGuestCartAsync(user.Id, ct);
                return RedirectToReturnUrlOrAccount(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty,
                    "Too many failed attempts. Your account is locked for 15 minutes — try again later or reset your password.");
                return LoginView(AuthPageViewModel.PaneSignIn, returnUrl, signIn: form);
            }
        }

        ModelState.AddModelError(string.Empty, "Incorrect email or password. Please try again.");
        return LoginView(AuthPageViewModel.PaneSignIn, returnUrl, signIn: form);
    }

    // ── POST /account/register ──────────────────────────────────────────
    [HttpPost("account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        [Bind(Prefix = "Register")] RegisterFormModel form,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        if (!form.AcceptTerms)
            ModelState.AddModelError("Register.AcceptTerms", "Please accept the Terms of Sale and Privacy Policy.");

        if (!ModelState.IsValid)
            return LoginView(AuthPageViewModel.PaneRegister, returnUrl, register: form);

        var email = form.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            PhoneNumber = $"{form.PhoneCountryCode}{form.Phone.Trim()}",
            MarketingEmailOptIn = form.EmailOptIn,
            MarketingWhatsAppOptIn = form.WhatsAppOptIn,
            LastLoginAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, form.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                // Username mirrors the email — DuplicateEmail already covers this case.
                if (error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                    continue;
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return LoginView(AuthPageViewModel.PaneRegister, returnUrl, register: form);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Customer);
        await _signInManager.SignInAsync(user, isPersistent: false);
        await MergeGuestCartAsync(user.Id, ct);

        return RedirectToReturnUrlOrAccount(returnUrl);
    }

    // ── POST /account/forgot ────────────────────────────────────────────
    [HttpPost("account/forgot")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Forgot(
        [Bind(Prefix = "Forgot")] ForgotPasswordFormModel form,
        string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return LoginView(AuthPageViewModel.PaneForgot, returnUrl, forgot: form);

        var email = form.Email.Trim();

        // Anti-enumeration: behave identically whether or not the account exists.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            // Token is generated now; the reset email itself ships in Phase 1C.
            _ = await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        TempData[ForgotSentToTempDataKey] = email;
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    // ── POST /account/logout ────────────────────────────────────────────
    [Authorize]
    [HttpPost("account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    /// <summary>Folds the guest cart (ws_cart cookie) into the user's cart after sign-in.</summary>
    private async Task MergeGuestCartAsync(Guid userId, CancellationToken ct)
    {
        var guestToken = CartCookieHelper.GetToken(HttpContext);
        if (guestToken is null)
            return;

        await _cartService.MergeGuestCartAsync(guestToken.Value, userId, ct);
        CartCookieHelper.Clear(HttpContext);
    }

    private IActionResult RedirectToReturnUrlOrAccount(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : Redirect("/account");

    private ViewResult LoginView(
        string activePane,
        string? returnUrl,
        SignInFormModel? signIn = null,
        RegisterFormModel? register = null,
        ForgotPasswordFormModel? forgot = null)
    {
        var model = new AuthPageViewModel
        {
            ActivePane = activePane,
            ReturnUrl = returnUrl,
            SignIn = signIn ?? new SignInFormModel(),
            Register = register ?? new RegisterFormModel(),
            Forgot = forgot ?? new ForgotPasswordFormModel()
        };
        return View(LoginViewPath, model);
    }
}
