using System.ComponentModel.DataAnnotations;

namespace WhiteStiches.Web.Models.Auth;

/// <summary>Forgot-password pane form on /account/login. Posted with the "Forgot" prefix.</summary>
public class ForgotPasswordFormModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}
