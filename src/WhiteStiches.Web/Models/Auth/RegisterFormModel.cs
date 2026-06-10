using System.ComponentModel.DataAnnotations;

namespace WhiteStiches.Web.Models.Auth;

/// <summary>Create-account pane form on /account/login. Posted with the "Register" prefix.</summary>
public class RegisterFormModel
{
    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(64, ErrorMessage = "First name is too long.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(64, ErrorMessage = "Last name is too long.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Dial code from the country selector. Defaults to Kuwait.</summary>
    public string PhoneCountryCode { get; set; } = "+965";

    [Required(ErrorMessage = "Please enter your mobile number.")]
    [StringLength(20, ErrorMessage = "Mobile number is too long.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Marketing consent. The page shows one combined checkbox covering email and WhatsApp.</summary>
    public bool EmailOptIn { get; set; }

    public bool WhatsAppOptIn { get; set; }

    /// <summary>Terms of Sale + Privacy Policy consent — validated in the controller.</summary>
    public bool AcceptTerms { get; set; }
}
