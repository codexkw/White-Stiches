namespace WhiteStiches.Web.Models.Auth;

/// <summary>
/// Page model for /account/login — carries all three panes (sign in / register / forgot)
/// plus which pane should be active after a server round-trip.
/// </summary>
public class AuthPageViewModel
{
    public const string PaneSignIn = "signin";
    public const string PaneRegister = "register";
    public const string PaneForgot = "forgot";

    public SignInFormModel SignIn { get; set; } = new();
    public RegisterFormModel Register { get; set; } = new();
    public ForgotPasswordFormModel Forgot { get; set; } = new();

    /// <summary>"signin" | "register" | "forgot" — pane to activate on load.</summary>
    public string ActivePane { get; set; } = PaneSignIn;

    public string? ReturnUrl { get; set; }

    /// <summary>True after a forgot-password submission — shows the "check your inbox" state.</summary>
    public bool ForgotSuccess { get; set; }

    /// <summary>Email echoed back in the forgot-password success message.</summary>
    public string? ForgotSentTo { get; set; }
}
