using System.ComponentModel.DataAnnotations;

namespace WhiteStiches.Web.Models.Auth;

/// <summary>Sign-in pane form on /account/login. Posted with the "SignIn" prefix.</summary>
public class SignInFormModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
