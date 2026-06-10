using System.ComponentModel.DataAnnotations;

namespace WhiteStiches.Web.Models.Checkout;

/// <summary>Form fields posted to POST /checkout/place — names match the static checkout form.</summary>
public class CheckoutFormModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Governorate is required.")]
    public string Governorate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area is required.")]
    public string Area { get; set; } = string.Empty;

    [Required(ErrorMessage = "Block is required.")]
    public string Block { get; set; } = string.Empty;

    [Required(ErrorMessage = "Street is required.")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Building / house is required.")]
    public string Building { get; set; } = string.Empty;

    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Directions { get; set; }

    [Required(ErrorMessage = "Choose a delivery method.")]
    [RegularExpression("^(standard|express|same-day)$", ErrorMessage = "Choose a valid delivery method.")]
    public string ShippingMethod { get; set; } = "standard";

    [Required(ErrorMessage = "Choose a payment method.")]
    [RegularExpression("^(knet|card|applepay)$", ErrorMessage = "Choose a valid payment method.")]
    public string PaymentMethod { get; set; } = "knet";

    public bool TermsAccepted { get; set; }

    public string? Note { get; set; }
}
