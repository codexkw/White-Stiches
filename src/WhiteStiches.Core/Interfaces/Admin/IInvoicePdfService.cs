using WhiteStiches.Core.Entities.Orders;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>Renders a downloadable PDF invoice for a placed order (admin order-detail download).</summary>
public interface IInvoicePdfService
{
    /// <summary>Build a one-page PDF invoice (with pricing). Pure render — the order graph must already be loaded.</summary>
    byte[] Build(Order order, InvoiceBranding branding);

    /// <summary>
    /// Build a one-page delivery note for the courier: same layout as the invoice but with NO pricing
    /// (no unit/amount columns, no totals) plus a received-by/signature line. Pure render.
    /// </summary>
    byte[] BuildDeliveryNote(Order order, InvoiceBranding branding);
}

/// <summary>Store branding + payment summary supplied by the caller (resolved from settings).</summary>
public sealed record InvoiceBranding(
    string StoreName,
    string? LogoPath,
    string? ContactEmail,
    string? ContactPhone,
    decimal TotalPaid,
    decimal TotalRefunded);
