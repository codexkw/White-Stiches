using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Interfaces.Admin;

namespace WhiteStiches.Infrastructure.Services.Admin;

/// <summary>
/// QuestPDF-based invoice renderer. English, Latin numerals + dot decimals (matches the house money
/// convention), single A4 page. Uses QuestPDF's embedded Lato font so it renders identically on
/// Windows/IIS and minimal Linux containers without any system-font dependency.
/// </summary>
public sealed class InvoicePdfService : IInvoicePdfService
{
    private const string Ink = "#0A0A0A";
    private const string Muted = "#6B6B6B";
    private const string Hair = "#E2E2E2";
    private const string Soft = "#F6F6F4";

    public byte[] Build(Order order, InvoiceBranding b)
    {
        var currency = string.IsNullOrWhiteSpace(order.Currency) ? "KWD" : order.Currency.Trim();
        string Money(decimal v) => v.ToString("0.000", CultureInfo.InvariantCulture) + " " + currency;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink).LineHeight(1.25f));

                page.Header().Element(c => Header(c, order, b));
                page.Content().PaddingVertical(16).Element(c => Body(c, order, b, Money));
                page.Footer().Element(c => Footer(c, b));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, Order o, InvoiceBranding b)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(b.StoreName).FontSize(18).Bold().FontColor(Ink).LetterSpacing(0.02f);
                col.Item().PaddingTop(2).Text("TAX INVOICE / RECEIPT").FontSize(8).FontColor(Muted).LetterSpacing(0.12f);
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text(o.OrderNumber).FontSize(16).Bold();
                col.Item().Text((o.PlacedAtUtc ?? o.CreatedAtUtc).ToString("yyyy-MM-dd HH:mm") + " UTC")
                    .FontSize(9).FontColor(Muted);
                col.Item().PaddingTop(2).Text($"{o.Status} · {o.PaymentStatus}").FontSize(8.5f).Bold().FontColor(Muted);
            });
        });
    }

    private static void Body(IContainer container, Order o, InvoiceBranding b, Func<decimal, string> money)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            col.Item().LineHorizontal(1).LineColor(Hair);

            col.Item().Row(row =>
            {
                row.Spacing(16);
                row.RelativeItem().Element(c => Party(c, "Bill to", BillTo(o)));
                row.RelativeItem().Element(c => Party(c, "Ship to", ShipTo(o)));
            });

            col.Item().Element(c => Items(c, o, money));

            col.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(250).Element(c => Totals(c, o, b, money));
            });

            if (!string.IsNullOrWhiteSpace(o.CustomerNote))
            {
                col.Item().Column(note =>
                {
                    note.Item().Text("CUSTOMER NOTE").FontSize(8).Bold().FontColor(Muted).LetterSpacing(0.08f);
                    note.Item().PaddingTop(2).Text(o.CustomerNote).FontSize(9);
                });
            }
        });
    }

    private static void Party(IContainer container, string title, IEnumerable<string> lines)
    {
        container.Border(1).BorderColor(Hair).Padding(10).Column(col =>
        {
            col.Item().Text(title.ToUpperInvariant()).FontSize(8).Bold().FontColor(Muted).LetterSpacing(0.08f);
            col.Item().PaddingTop(4).Column(inner =>
            {
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                    inner.Item().Text(line).FontSize(9);
            });
        });
    }

    private static void Items(IContainer container, Order o, Func<decimal, string> money)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);   // #
                cols.RelativeColumn();      // item
                cols.ConstantColumn(34);    // qty
                cols.ConstantColumn(80);    // unit
                cols.ConstantColumn(84);    // amount
            });

            table.Header(header =>
            {
                header.Cell().Element(HeadCell).Text("#");
                header.Cell().Element(HeadCell).Text("Item");
                header.Cell().Element(HeadCell).AlignRight().Text("Qty");
                header.Cell().Element(HeadCell).AlignRight().Text("Unit");
                header.Cell().Element(HeadCell).AlignRight().Text("Amount");
            });

            var idx = 1;
            foreach (var it in o.Items)
            {
                table.Cell().Element(BodyCell).Text(idx.ToString(CultureInfo.InvariantCulture)).FontColor(Muted);
                table.Cell().Element(BodyCell).Column(cell =>
                {
                    cell.Item().Text(Title(it)).FontSize(9);
                    var sub = new List<string>();
                    if (!string.IsNullOrWhiteSpace(it.VariantDescription)) sub.Add(it.VariantDescription!);
                    if (!string.IsNullOrWhiteSpace(it.Sku)) sub.Add("SKU " + it.Sku);
                    if (sub.Count > 0)
                        cell.Item().Text(string.Join("   ·   ", sub)).FontSize(7.5f).FontColor(Muted);
                });
                table.Cell().Element(BodyCell).AlignRight().Text(it.Quantity.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(money(it.UnitPrice));
                table.Cell().Element(BodyCell).AlignRight().Text(money(it.LineTotal));
                idx++;
            }
        });

        static IContainer HeadCell(IContainer c) =>
            c.Background(Soft).PaddingVertical(6).PaddingHorizontal(6)
                .DefaultTextStyle(t => t.FontSize(8).Bold().FontColor(Muted));

        static IContainer BodyCell(IContainer c) =>
            c.BorderBottom(1).BorderColor(Hair).PaddingVertical(6).PaddingHorizontal(6);
    }

    private static void Totals(IContainer container, Order o, InvoiceBranding b, Func<decimal, string> money)
    {
        container.Column(col =>
        {
            col.Spacing(3);

            void Line(string label, string value)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(label).FontSize(9).FontColor(Muted);
                    row.AutoItem().Text(value).FontSize(9);
                });
            }

            Line("Subtotal", money(o.Subtotal));
            if (o.DiscountAmount > 0)
                Line("Discount" + (string.IsNullOrWhiteSpace(o.DiscountCodeSnapshot) ? "" : $" ({o.DiscountCodeSnapshot})"),
                    "−" + money(o.DiscountAmount));
            if (o.ShippingAmount > 0 || !string.IsNullOrWhiteSpace(o.ShippingMethodName))
                Line("Shipping" + (string.IsNullOrWhiteSpace(o.ShippingMethodName) ? "" : $" ({o.ShippingMethodName})"),
                    money(o.ShippingAmount));
            if (o.GiftWrapFee > 0) Line("Gift wrap", money(o.GiftWrapFee));
            if (o.TaxAmount > 0) Line("Tax", money(o.TaxAmount));

            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Hair);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total").FontSize(11).Bold();
                row.AutoItem().Text(money(o.Total)).FontSize(11).Bold();
            });

            if (b.TotalPaid > 0) Line("Paid", money(b.TotalPaid));
            if (b.TotalRefunded > 0) Line("Refunded", "−" + money(b.TotalRefunded));

            // Balance owed BY the customer = total minus what they've paid. Refunds are a separate
            // outflow back to the customer (shown above) and must not inflate the amount-due line.
            var balance = o.Total - b.TotalPaid;
            if (balance > 0.0009m)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Balance due").FontSize(9.5f).Bold().FontColor(Ink);
                    row.AutoItem().Text(money(balance)).FontSize(9.5f).Bold();
                });
            }
        });
    }

    private static void Footer(IContainer container, InvoiceBranding b)
    {
        container.BorderTop(1).BorderColor(Hair).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Thank you for your order.").FontSize(8.5f).FontColor(Muted);
                var contact = new List<string>();
                if (!string.IsNullOrWhiteSpace(b.ContactEmail)) contact.Add(b.ContactEmail!);
                if (!string.IsNullOrWhiteSpace(b.ContactPhone)) contact.Add(b.ContactPhone!);
                if (contact.Count > 0)
                    col.Item().Text(string.Join("   ·   ", contact)).FontSize(8).FontColor(Muted);
            });

            row.AutoItem().AlignBottom().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    // Always non-empty: QuestPDF's Text() rejects/zero-renders an empty string, so guard data gaps.
    private static string Title(OrderItem it) =>
        !string.IsNullOrWhiteSpace(it.TitleEn) ? it.TitleEn
        : !string.IsNullOrWhiteSpace(it.TitleAr) ? it.TitleAr
        : $"Item #{it.Id}";

    private static string CustomerName(Order o)
    {
        var name = $"{o.ShipFirstName} {o.ShipLastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "(no name)" : name;
    }

    private static IEnumerable<string> BillTo(Order o)
    {
        yield return CustomerName(o);
        if (!string.IsNullOrWhiteSpace(o.Email)) yield return o.Email;
        if (!string.IsNullOrWhiteSpace(o.Phone)) yield return o.Phone;
    }

    private static IEnumerable<string> ShipTo(Order o)
    {
        yield return CustomerName(o);

        var locality = new[] { o.ShipBlock is { Length: > 0 } ? "Block " + o.ShipBlock : null,
                               o.ShipStreet is { Length: > 0 } ? "Street " + o.ShipStreet : null,
                               o.ShipBuilding is { Length: > 0 } ? "Building " + o.ShipBuilding : null }
            .Where(s => s is not null);
        var localityLine = string.Join(", ", locality);
        if (!string.IsNullOrWhiteSpace(localityLine)) yield return localityLine;

        var unit = new[] { string.IsNullOrWhiteSpace(o.ShipFloor) ? null : "Floor " + o.ShipFloor,
                           string.IsNullOrWhiteSpace(o.ShipApartment) ? null : "Apt " + o.ShipApartment }
            .Where(s => s is not null);
        var unitLine = string.Join(", ", unit);
        if (!string.IsNullOrWhiteSpace(unitLine)) yield return unitLine;

        var area = new[] { o.ShipArea, o.ShipGovernorate }.Where(s => !string.IsNullOrWhiteSpace(s));
        var areaLine = string.Join(", ", area);
        if (!string.IsNullOrWhiteSpace(areaLine)) yield return areaLine;

        if (!string.IsNullOrWhiteSpace(o.ShipCountry)) yield return o.ShipCountry;
        if (!string.IsNullOrWhiteSpace(o.ShipDirections)) yield return o.ShipDirections!;
    }
}
