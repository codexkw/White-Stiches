using System.ComponentModel.DataAnnotations;

namespace WhiteStiches.Web.Models.Auth;

/// <summary>Standalone reset-password page (/account/reset-password), reached from the email link.</summary>
public class ResetPasswordFormModel
{
    /// <summary>Base64Url-encoded Identity reset token from the email link.</summary>
    public string? Token { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a new password.")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>True when the link is missing its token/email — show the "request a new link" state.</summary>
    public bool LinkInvalid { get; set; }

    /// <summary>True once the password has been changed — show the success state.</summary>
    public bool Success { get; set; }
}
