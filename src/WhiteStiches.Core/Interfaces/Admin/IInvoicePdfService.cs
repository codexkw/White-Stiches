using WhiteStiches.Core.Entities.Orders;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>Renders a downloadable PDF invoice for a placed order (admin order-detail download).</summary>
public interface IInvoicePdfService
{
    /// <summary>Build a one-page PDF invoice. Pure render — the order graph must already be loaded.</summary>
    byte[] Build(Order order, InvoiceBranding branding);
}

/// <summary>Store branding + payment summary supplied by the caller (resolved from settings).</summary>
public sealed record InvoiceBranding(
    string StoreName,
    string? LogoPath,
    string? ContactEmail,
    string? ContactPhone,
    decimal TotalPaid,
    decimal TotalRefunded);
