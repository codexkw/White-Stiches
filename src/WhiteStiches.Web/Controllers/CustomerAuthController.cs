using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Infrastructure.Localization;
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
    private const string ResetViewPath = "~/Views/Account/ResetPassword.cshtml";
    private const string ForgotSentToTempDataKey = "AuthForgotSentTo";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartService _cartService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public CustomerAuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ICartService cartService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _cartService = cartService;
        _emailService = emailService;
        _configuration = configuration;
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
                // Their saved language wins on sign-in (LOC-02).
                WhiteStichesLocalization.WriteCultureCookie(HttpContext, user.PreferredLanguage);
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
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return LoginView(AuthPageViewModel.PaneForgot, returnUrl, forgot: form);

        var email = form.Email.Trim();

        // Anti-enumeration: behave identically whether or not the account exists.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetLink = BuildResetLink(encoded, email);
            await _emailService.SendPasswordResetAsync(user.Email ?? email, user.FullName,
                user.PreferredLanguage, resetLink, ct);
        }

        TempData[ForgotSentToTempDataKey] = email;
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    // ── GET /account/reset-password ─────────────────────────────────────
    [HttpGet("account/reset-password")]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/account");

        return View(ResetViewPath, new ResetPasswordFormModel
        {
            Token = token,
            Email = email ?? string.Empty,
            LinkInvalid = string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email)
        });
    }

    // ── POST /account/reset-password ────────────────────────────────────
    [HttpPost("account/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Token) || string.IsNullOrWhiteSpace(form.Email))
        {
            form.LinkInvalid = true;
            return View(ResetViewPath, form);
        }

        if (!ModelState.IsValid)
            return View(ResetViewPath, form);

        var user = await _userManager.FindByEmailAsync(form.Email.Trim());
        if (user is not null)
        {
            string decoded;
            try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(form.Token)); }
            catch { decoded = form.Token; }

            var result = await _userManager.ResetPasswordAsync(user, decoded, form.Password);
            if (result.Succeeded)
            {
                form.Success = true;
                return View(ResetViewPath, form);
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }
        else
        {
            // Don't reveal whether the email exists; a bad/expired link looks the same.
            ModelState.AddModelError(string.Empty,
                "This reset link is invalid or has expired. Please request a new one.");
        }

        return View(ResetViewPath, form);
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

    /// <summary>
    /// Builds the absolute reset-password URL for the email. Prefers a configured public origin
    /// (Smtp:BaseUrl → Tap:PublicBaseUrl) so links are correct behind a TLS proxy; falls back to
    /// the current request's scheme/host for local dev.
    /// </summary>
    private string BuildResetLink(string token, string email)
    {
        var path = Url.Action(nameof(ResetPassword), "CustomerAuth", new { token, email })!;
        var baseUrl = _configuration["Smtp:BaseUrl"] ?? _configuration["Tap:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl)
            ? $"{Request.Scheme}://{Request.Host}{path}"
            : new Uri(new Uri(baseUrl), path).ToString();
    }

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
