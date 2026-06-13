using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Customers;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Account;

/// <summary>Shared data for the account sidebar shown on every account page.</summary>
public abstract class AccountPageModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ─── Page view models ────────────────────────────────────────────────────────

public class DashboardViewModel : AccountPageModel
{
    public string FirstName { get; set; } = string.Empty;
    public DateTime MemberSinceUtc { get; set; }
    public string MemberSinceText => MemberSinceUtc.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public IReadOnlyList<Order> RecentOrders { get; set; } = [];
    public int OpenOrderCount { get; set; }
    public int TotalOrderCount { get; set; }

    public Address? DefaultAddress { get; set; }

    public IReadOnlyList<Product> WishlistPreview { get; set; } = [];
    public int WishlistCount { get; set; }
    public int WishlistOnSaleCount { get; set; }
}

public class OrdersViewModel : AccountPageModel
{
    public PagedResult<Order> Orders { get; set; } = new();
    public DateTime MemberSinceUtc { get; set; }
    public string MemberSinceText => MemberSinceUtc.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}

public class OrderDetailViewModel : AccountPageModel
{
    public Order Order { get; set; } = null!;
}

public class AddressesViewModel : AccountPageModel
{
    public IReadOnlyList<Address> Addresses { get; set; } = [];

    /// <summary>When set, the form card renders open pre-filled for this address.</summary>
    public Address? EditAddress { get; set; }
}

public class ProfileViewModel : AccountPageModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "KWD";
    public bool MarketingEmailOptIn { get; set; }
    public bool MarketingSmsOptIn { get; set; }
    public bool MarketingWhatsAppOptIn { get; set; }
}

public class WishlistViewModel : AccountPageModel
{
    public IReadOnlyList<Product> Products { get; set; } = [];
    public int OnSaleCount { get; set; }
}

public class ReturnsViewModel : AccountPageModel
{
    public IReadOnlyList<ReturnRequest> Returns { get; set; } = [];

    /// <summary>Delivered orders that can start a return.</summary>
    public IReadOnlyList<Order> EligibleOrders { get; set; } = [];

    /// <summary>When set, the "new return" wizard section renders for this order.</summary>
    public Order? NewReturnOrder { get; set; }
}

// ─── POST input models ───────────────────────────────────────────────────────

public class AddressInputModel
{
    public int Id { get; set; }
    public string? Label { get; set; }

    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string Country { get; set; } = "KW";
    [Required] public string Governorate { get; set; } = string.Empty;
    [Required] public string Area { get; set; } = string.Empty;
    [Required] public string Block { get; set; } = string.Empty;
    [Required] public string Street { get; set; } = string.Empty;
    [Required] public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Directions { get; set; }

    public bool IsDefault { get; set; }
}

public class ProfileInputModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "KWD";
    public bool MarketingEmailOptIn { get; set; }
    public bool MarketingSmsOptIn { get; set; }
    public bool MarketingWhatsAppOptIn { get; set; }
}

public class PasswordInputModel
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ReturnCreateInputModel
{
    [Required] public string OrderNumber { get; set; } = string.Empty;
    public List<ReturnCreateItemModel> Items { get; set; } = [];

    /// <summary>"pickup" or "dropoff".</summary>
    public string Method { get; set; } = "pickup";
    public string? CustomerReason { get; set; }
}

public class ReturnCreateItemModel
{
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
}

// ─── Display helpers (badge classes match the static site) ──────────────────

public static class AccountFormat
{
    public static string Money(decimal value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture) + " KWD";

    public static string Date(DateTime? utc) =>
        utc?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) ?? "—";

    /// <summary>Maps an order status to the static site's status-badge modifier + label.</summary>
    public static (string Css, string Label) OrderBadge(OrderStatus status) => status switch
    {
        OrderStatus.Placed => ("status-badge--pending", "Placed"),
        OrderStatus.Paid => ("status-badge--approved", "Paid"),
        OrderStatus.Fulfilled => ("status-badge--in-transit", "Fulfilled"),
        OrderStatus.Shipped => ("status-badge--in-transit", "In transit"),
        OrderStatus.Delivered => ("status-badge--delivered", "Delivered"),
        OrderStatus.Cancelled => ("status-badge--cancelled", "Cancelled"),
        OrderStatus.Refunded => ("status-badge--delivered", "Refunded"),
        _ => ("status-badge--pending", status.ToString())
    };

    /// <summary>Maps a return status to the static site's status-badge modifier + label.</summary>
    public static (string Css, string Label) ReturnBadge(ReturnStatus status) => status switch
    {
        ReturnStatus.Pending => ("status-badge--pending", "Pending review"),
        ReturnStatus.Approved => ("status-badge--approved", "Approved · ship to us"),
        ReturnStatus.Received => ("status-badge--in-transit", "Received"),
        ReturnStatus.Refunded => ("status-badge--delivered", "Refunded"),
        ReturnStatus.Rejected => ("status-badge--cancelled", "Rejected"),
        _ => ("status-badge--pending", status.ToString())
    };

    /// <summary>Payment method key → display label ("knet" → "KNET").</summary>
    public static string PaymentMethod(string method) => method.ToLowerInvariant() switch
    {
        "knet" => "KNET",
        "visa" => "Visa",
        "mastercard" => "Mastercard",
        "applepay" => "Apple Pay",
        "googlepay" => "Google Pay",
        "cod" => "Cash on delivery",
        "tap" => "Tap Payments",
        "" => "Card",
        _ => method
    };

    public static string TransactionLabel(TransactionStatus status) => status switch
    {
        TransactionStatus.Initiated => "Initiated",
        TransactionStatus.Authorized => "Authorized",
        TransactionStatus.Captured => "Captured",
        TransactionStatus.Failed => "Failed",
        TransactionStatus.Voided => "Voided",
        TransactionStatus.Refunded => "Refunded",
        _ => status.ToString()
    };

    /// <summary>The reasons offered in the return wizard (mirrors the static site's examples).</summary>
    public static readonly string[] ReturnReasons =
    [
        "Wrong size",
        "Didn't fit",
        "Defective",
        "Not as pictured",
        "Changed my mind"
    ];

    public static readonly string[] Governorates =
    [
        "Al Asimah",
        "Hawalli",
        "Al Farwaniyah",
        "Mubarak Al-Kabeer",
        "Al Ahmadi",
        "Al Jahra"
    ];
}
