using System.Globalization;
using System.Net;
using System.Text;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;

namespace WhiteStiches.Infrastructure.Email;

/// <summary>
/// Dependency-free, email-client-safe HTML templates for the transactional messages (Phase 1C-3).
/// Bilingual: pass <c>lang = "ar"</c> for Arabic copy + RTL layout, anything else for English.
/// Money is always rendered Latin / invariant (KWD 3-decimal) to match the house convention.
/// </summary>
public static class EmailTemplates
{
    private const string Ink = "#141414";
    private const string Muted = "#6b6b6b";
    private const string Line = "#e6e6e6";
    private const string Bg = "#f5f3f0";

    private static bool IsAr(string lang) => string.Equals(lang, "ar", StringComparison.OrdinalIgnoreCase);

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private static string Money(decimal v, string currency) =>
        $"{v.ToString("0.000", CultureInfo.InvariantCulture)} {Enc(currency)}";

    private static string ItemTitle(OrderItem i, bool ar) =>
        ar && !string.IsNullOrWhiteSpace(i.TitleAr) ? i.TitleAr : i.TitleEn;

    // ── Public template builders ─────────────────────────────────────────────

    public static (string Subject, string Html) PasswordReset(string storeName, string? name, string resetLink, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar ? $"إعادة تعيين كلمة المرور - {storeName}" : $"Reset your password - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(name)}،" : $"Hi {Enc(name)},";
        var intro = ar
            ? "تلقّينا طلباً لإعادة تعيين كلمة المرور الخاصة بحسابك. اضغط الزر أدناه لاختيار كلمة مرور جديدة."
            : "We received a request to reset your account password. Tap the button below to choose a new one.";
        var button = ar ? "إعادة تعيين كلمة المرور" : "Reset password";
        var expiry = ar
            ? "هذا الرابط صالح لفترة محدودة. إذا لم تطلب إعادة التعيين، يمكنك تجاهل هذه الرسالة بأمان."
            : "This link is valid for a limited time. If you didn't request a reset, you can safely ignore this email.";

        var inner = new StringBuilder()
            .Append(P(greeting, ar))
            .Append(P(intro, ar))
            .Append(Button(button, resetLink, ar))
            .Append(P(expiry, ar, muted: true))
            .ToString();

        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) OrderConfirmation(string storeName, Order order, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تأكيد الطلب {order.OrderNumber} - {storeName}"
            : $"Order {order.OrderNumber} confirmed - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"شكراً لطلبك! لقد استلمنا طلبك رقم <strong>{Enc(order.OrderNumber)}</strong> وسنعلمك عند شحنه."
            : $"Thank you for your order! We've received order <strong>{Enc(order.OrderNumber)}</strong> and will let you know when it ships.";

        var inner = new StringBuilder()
            .Append(P(greeting, ar))
            .Append(P(intro, ar))
            .Append(ItemsTable(order, ar))
            .Append(Totals(order, ar))
            .Append(AddressBlock(order, ar))
            .ToString();

        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) OrderShipped(string storeName, Order order, Shipment shipment, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تم شحن طلبك {order.OrderNumber} - {storeName}"
            : $"Your order {order.OrderNumber} has shipped - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"أخبار رائعة — طلبك رقم <strong>{Enc(order.OrderNumber)}</strong> في طريقه إليك."
            : $"Great news — your order <strong>{Enc(order.OrderNumber)}</strong> is on its way.";

        var details = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(shipment.Carrier))
            details.Append(KeyVal(ar ? "شركة الشحن" : "Carrier", Enc(shipment.Carrier), ar));
        if (!string.IsNullOrWhiteSpace(shipment.AwbNumber))
            details.Append(KeyVal(ar ? "رقم التتبّع" : "Tracking number", Enc(shipment.AwbNumber), ar));

        var inner = new StringBuilder()
            .Append(P(greeting, ar))
            .Append(P(intro, ar))
            .Append(details.Length > 0 ? Card(details.ToString()) : string.Empty);

        if (!string.IsNullOrWhiteSpace(shipment.TrackingUrl))
            inner.Append(Button(ar ? "تتبّع الشحنة" : "Track shipment", shipment.TrackingUrl!, ar));

        inner.Append(ItemsTable(order, ar));

        return (subject, Shell(storeName, ar, inner.ToString(), subject));
    }

    public static (string Subject, string Html) OrderRefunded(string storeName, Order order, Refund refund, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تم استرداد مبلغ لطلبك {order.OrderNumber} - {storeName}"
            : $"Refund issued for order {order.OrderNumber} - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"لقد قمنا باسترداد مبلغ لطلبك رقم <strong>{Enc(order.OrderNumber)}</strong>."
            : $"We've issued a refund for your order <strong>{Enc(order.OrderNumber)}</strong>.";
        var card = Card(
            KeyVal(ar ? "المبلغ المسترد" : "Refund amount", Money(refund.Amount, order.Currency), ar)
            + (string.IsNullOrWhiteSpace(refund.Reason) ? string.Empty
                : KeyVal(ar ? "السبب" : "Reason", Enc(refund.Reason), ar)));
        var note = ar
            ? "قد يستغرق ظهور المبلغ في كشف حسابك من 5 إلى 10 أيام عمل، حسب البنك."
            : "It can take 5–10 business days to appear on your statement, depending on your bank.";

        var inner = new StringBuilder()
            .Append(P(greeting, ar)).Append(P(intro, ar))
            .Append(card).Append(P(note, ar, muted: true))
            .ToString();

        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) OrderCancelled(string storeName, Order order, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تم إلغاء طلبك {order.OrderNumber} - {storeName}"
            : $"Your order {order.OrderNumber} was cancelled - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"نأسف لإبلاغك بأنه تم إلغاء طلبك رقم <strong>{Enc(order.OrderNumber)}</strong>."
            : $"We're writing to let you know that your order <strong>{Enc(order.OrderNumber)}</strong> has been cancelled.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar));
        if (!string.IsNullOrWhiteSpace(order.CancelReason))
            inner.Append(Card(KeyVal(ar ? "السبب" : "Reason", Enc(order.CancelReason), ar)));

        var charged = order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded;
        var refundNote = charged
            ? (ar ? "إذا كان قد تم خصم المبلغ، فسيتم رده إلى طريقة الدفع الأصلية."
                  : "If you were charged, a refund will be issued to your original payment method.")
            : (ar ? "لم يتم خصم أي مبلغ من حسابك لهذا الطلب."
                  : "You have not been charged for this order.");
        inner.Append(P(refundNote, ar, muted: true));

        return (subject, Shell(storeName, ar, inner.ToString(), subject));
    }

    public static (string Subject, string Html) OrderDelivered(string storeName, Order order, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تم توصيل طلبك {order.OrderNumber} - {storeName}"
            : $"Your order {order.OrderNumber} was delivered - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"تم توصيل طلبك رقم <strong>{Enc(order.OrderNumber)}</strong>. نتمنى أن ينال إعجابك!"
            : $"Your order <strong>{Enc(order.OrderNumber)}</strong> has been delivered. We hope you love it!";
        var help = ar
            ? "إذا كان هناك أي مشكلة في طلبك، تواصل معنا وسنكون سعداء بمساعدتك."
            : "If anything isn't right with your order, just reply or contact us — we're happy to help.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar)).Append(P(help, ar, muted: true)).ToString();
        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) ReturnRequested(string storeName, Order order, ReturnRequest request, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تم استلام طلب الإرجاع {request.RmaNumber} - {storeName}"
            : $"Return request {request.RmaNumber} received - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"لقد استلمنا طلب الإرجاع الخاص بك للطلب رقم <strong>{Enc(order.OrderNumber)}</strong>."
            : $"We've received your return request for order <strong>{Enc(order.OrderNumber)}</strong>.";
        var card = Card(KeyVal(ar ? "رقم الإرجاع" : "Return number", Enc(request.RmaNumber), ar));
        var next = ar
            ? "سيقوم فريقنا بمراجعة طلبك خلال 24 ساعة وسنرسل لك الخطوات التالية عبر البريد الإلكتروني."
            : "Our team will review it within 24 hours and email you the next steps.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar)).Append(card).Append(P(next, ar, muted: true)).ToString();
        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) ReturnApproved(string storeName, Order order, ReturnRequest request, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تمت الموافقة على إرجاعك {request.RmaNumber} - {storeName}"
            : $"Your return {request.RmaNumber} is approved - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"أخبار جيدة — تمت الموافقة على طلب الإرجاع <strong>{Enc(request.RmaNumber)}</strong>."
            : $"Good news — your return <strong>{Enc(request.RmaNumber)}</strong> has been approved.";

        var pickup = string.Equals(request.Method, "pickup", StringComparison.OrdinalIgnoreCase);
        var instr = pickup
            ? (ar ? "سنقوم بترتيب استلام القطعة (القطع) من عنوانك وسنتواصل معك لتحديد الموعد."
                  : "We'll arrange a pickup of the item(s) from your address and contact you to schedule it.")
            : (ar ? "يرجى إعادة القطعة (القطع) إلينا مع ذكر رقم الإرجاع أعلاه. سنبدأ في معالجة استردادك بمجرد وصولها."
                  : "Please return the item(s) to us quoting your return number above. We'll process your refund as soon as it arrives.");

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar)).Append(P(instr, ar));
        if (!string.IsNullOrWhiteSpace(request.StaffNote))
            inner.Append(Card(KeyVal(ar ? "ملاحظة" : "Note", Enc(request.StaffNote), ar)));

        return (subject, Shell(storeName, ar, inner.ToString(), subject));
    }

    public static (string Subject, string Html) ReturnRejected(string storeName, Order order, ReturnRequest request, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"تحديث بشأن طلب الإرجاع {request.RmaNumber} - {storeName}"
            : $"Update on your return {request.RmaNumber} - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"بعد المراجعة، لم نتمكّن من الموافقة على طلب الإرجاع <strong>{Enc(request.RmaNumber)}</strong>."
            : $"After review, we were unable to approve your return <strong>{Enc(request.RmaNumber)}</strong>.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar));
        if (!string.IsNullOrWhiteSpace(request.StaffNote))
            inner.Append(Card(KeyVal(ar ? "السبب" : "Reason", Enc(request.StaffNote), ar)));
        var help = ar
            ? "إذا كان لديك أي استفسار، فلا تتردّد في التواصل معنا."
            : "If you have any questions, please get in touch — we're happy to help.";
        inner.Append(P(help, ar, muted: true));

        return (subject, Shell(storeName, ar, inner.ToString(), subject));
    }

    public static (string Subject, string Html) ReturnReceived(string storeName, Order order, ReturnRequest request, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"استلمنا إرجاعك {request.RmaNumber} - {storeName}"
            : $"We received your return {request.RmaNumber} - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"لقد استلمنا القطعة (القطع) المُرجعة ضمن طلب الإرجاع <strong>{Enc(request.RmaNumber)}</strong>، ونقوم الآن بمعالجة استردادك."
            : $"We've received the returned item(s) for <strong>{Enc(request.RmaNumber)}</strong> and are now processing your refund.";
        var next = ar
            ? "سنرسل لك تأكيداً بمجرّد إصدار المبلغ المسترد."
            : "You'll get a confirmation as soon as the refund is issued.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar)).Append(P(next, ar, muted: true)).ToString();
        return (subject, Shell(storeName, ar, inner, subject));
    }

    public static (string Subject, string Html) PaymentFailed(string storeName, Order order, string? retryUrl, string lang)
    {
        var ar = IsAr(lang);
        var subject = ar
            ? $"لم تكتمل عملية الدفع لطلبك {order.OrderNumber} - {storeName}"
            : $"Your payment didn't go through - order {order.OrderNumber} - {storeName}";

        var greeting = ar ? $"مرحباً {Enc(order.ShipFirstName)}،" : $"Hi {Enc(order.ShipFirstName)},";
        var intro = ar
            ? $"لم نتمكّن من إتمام عملية الدفع لطلبك رقم <strong>{Enc(order.OrderNumber)}</strong>، لذا لم يتم تأكيده بعد. لا تقلق — حقيبتك محفوظة."
            : $"We couldn't complete the payment for your order <strong>{Enc(order.OrderNumber)}</strong>, so it isn't confirmed yet. Don't worry — your bag is saved.";

        var inner = new StringBuilder().Append(P(greeting, ar)).Append(P(intro, ar));
        if (!string.IsNullOrWhiteSpace(retryUrl))
            inner.Append(Button(ar ? "أكمل طلبك" : "Complete your order", retryUrl!, ar));
        var help = ar
            ? "إذا واجهت أي مشكلة، تواصل معنا وسنساعدك."
            : "If you run into any trouble, contact us and we'll help.";
        inner.Append(P(help, ar, muted: true));

        return (subject, Shell(storeName, ar, inner.ToString(), subject));
    }

    /// <summary>Internal staff alert (English only). Renders a bold heading + a Card of key/value rows.</summary>
    public static (string Subject, string Html) AdminAlert(string storeName, string subject, string heading,
        IReadOnlyList<(string Key, string Value)> rows)
    {
        var body = new StringBuilder();
        foreach (var (k, v) in rows) body.Append(KeyVal(k, Enc(v), ar: false));
        var inner = new StringBuilder()
            .Append(P($"<strong>{Enc(heading)}</strong>", ar: false))
            .Append(Card(body.ToString()))
            .ToString();
        return (subject, Shell(storeName, ar: false, inner, subject));
    }

    // ── Building blocks ──────────────────────────────────────────────────────

    private static string Shell(string storeName, bool ar, string inner, string preheader)
    {
        var dir = ar ? "rtl" : "ltr";
        var align = ar ? "right" : "left";
        var font = ar
            ? "'Segoe UI','Tahoma',Arial,sans-serif"
            : "'Helvetica Neue',Helvetica,Arial,sans-serif";
        var footer = ar
            ? "هذه رسالة آلية، يُرجى عدم الرد عليها."
            : "This is an automated message, please do not reply.";

        return $@"<!DOCTYPE html>
<html lang=""{(ar ? "ar" : "en")}"" dir=""{dir}"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:{Bg};"">
<span style=""display:none;max-height:0;overflow:hidden;opacity:0;"">{Enc(preheader)}</span>
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{Bg};padding:24px 0;"">
  <tr><td align=""center"">
    <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;background:#ffffff;border:1px solid {Line};"">
      <tr><td style=""background:{Ink};padding:22px 28px;text-align:center;"">
        <span style=""color:#ffffff;font-family:{font};font-size:20px;letter-spacing:3px;text-transform:uppercase;"">{Enc(storeName)}</span>
      </td></tr>
      <tr><td dir=""{dir}"" style=""padding:28px;font-family:{font};color:{Ink};font-size:15px;line-height:1.6;text-align:{align};"">
        {inner}
      </td></tr>
      <tr><td style=""padding:18px 28px;border-top:1px solid {Line};text-align:center;"">
        <span style=""font-family:{font};color:{Muted};font-size:12px;"">{Enc(storeName)} &middot; {footer}</span>
      </td></tr>
    </table>
  </td></tr>
</table>
</body></html>";
    }

    private static string P(string html, bool ar, bool muted = false)
    {
        var color = muted ? Muted : Ink;
        var align = ar ? "right" : "left";
        return $@"<p style=""margin:0 0 14px;color:{color};text-align:{align};"">{html}</p>";
    }

    private static string Button(string label, string href, bool ar) => $@"
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:18px 0;""><tr>
  <td style=""background:{Ink};""><a href=""{Enc(href)}"" style=""display:inline-block;padding:13px 30px;color:#ffffff;font-size:14px;text-decoration:none;letter-spacing:1px;text-transform:uppercase;"">{Enc(label)}</a></td>
</tr></table>";

    private static string Card(string inner) =>
        $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:8px 0 18px;background:{Bg};border:1px solid {Line};""><tr><td style=""padding:14px 16px;"">{inner}</td></tr></table>";

    private static string KeyVal(string key, string val, bool ar)
    {
        var align = ar ? "right" : "left";
        return $@"<div style=""margin:2px 0;text-align:{align};""><span style=""color:{Muted};"">{Enc(key)}:</span> <strong>{val}</strong></div>";
    }

    private static string ItemsTable(Order order, bool ar)
    {
        var align = ar ? "right" : "left";
        var opp = ar ? "left" : "right";
        var hQty = ar ? "الكمية" : "Qty";
        var hItem = ar ? "المنتج" : "Item";
        var hTotal = ar ? "الإجمالي" : "Total";

        var rows = new StringBuilder();
        foreach (var i in order.Items)
        {
            var variant = string.IsNullOrWhiteSpace(i.VariantDescription)
                ? string.Empty
                : $@"<br><span style=""color:{Muted};font-size:13px;"">{Enc(i.VariantDescription)}</span>";
            rows.Append($@"<tr>
  <td style=""padding:10px 0;border-bottom:1px solid {Line};text-align:{align};"">{Enc(ItemTitle(i, ar))}{variant}</td>
  <td style=""padding:10px 8px;border-bottom:1px solid {Line};text-align:center;color:{Muted};"">{i.Quantity.ToString(CultureInfo.InvariantCulture)}</td>
  <td style=""padding:10px 0;border-bottom:1px solid {Line};text-align:{opp};white-space:nowrap;"">{Money(i.LineTotal, order.Currency)}</td>
</tr>");
        }

        return $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:18px 0;font-size:14px;"">
  <tr>
    <th style=""padding:0 0 8px;border-bottom:2px solid {Ink};text-align:{align};font-weight:600;"">{hItem}</th>
    <th style=""padding:0 8px 8px;border-bottom:2px solid {Ink};text-align:center;font-weight:600;"">{hQty}</th>
    <th style=""padding:0 0 8px;border-bottom:2px solid {Ink};text-align:{opp};font-weight:600;"">{hTotal}</th>
  </tr>
  {rows}
</table>";
    }

    private static string Totals(Order order, bool ar)
    {
        var rows = new StringBuilder();
        rows.Append(TotalRow(ar ? "المجموع الفرعي" : "Subtotal", Money(order.Subtotal, order.Currency), ar));
        if (order.DiscountAmount > 0)
            rows.Append(TotalRow(ar ? "الخصم" : "Discount", "-" + Money(order.DiscountAmount, order.Currency), ar));
        rows.Append(TotalRow(ar ? "الشحن" : "Shipping", Money(order.ShippingAmount, order.Currency), ar));
        if (order.GiftWrapFee > 0)
            rows.Append(TotalRow(ar ? "تغليف الهدايا" : "Gift wrap", Money(order.GiftWrapFee, order.Currency), ar));
        if (order.TaxAmount > 0)
            rows.Append(TotalRow(ar ? "الضريبة" : "Tax", Money(order.TaxAmount, order.Currency), ar));
        rows.Append(TotalRow(ar ? "الإجمالي" : "Total", Money(order.Total, order.Currency), ar, bold: true));

        return $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 18px;font-size:14px;"">{rows}</table>";
    }

    private static string TotalRow(string label, string val, bool ar, bool bold = false)
    {
        var align = ar ? "right" : "left";
        var opp = ar ? "left" : "right";
        var weight = bold ? "font-weight:700;border-top:2px solid " + Ink + ";" : "color:" + Muted + ";";
        var pad = bold ? "10px 0 0;" : "4px 0;";
        return $@"<tr>
  <td style=""padding:{pad}text-align:{align};{weight}"">{Enc(label)}</td>
  <td style=""padding:{pad}text-align:{opp};white-space:nowrap;{(bold ? "font-weight:700;" : "")}"">{val}</td>
</tr>";
    }

    private static string AddressBlock(Order order, bool ar)
    {
        var title = ar ? "عنوان الشحن" : "Shipping to";
        var name = Enc($"{order.ShipFirstName} {order.ShipLastName}".Trim());
        var lineParts = new[]
        {
            order.ShipStreet, order.ShipBuilding, order.ShipBlock, order.ShipArea, order.ShipGovernorate
        }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Enc);
        var line = string.Join(", ", lineParts);
        var align = ar ? "right" : "left";

        return $@"<div style=""margin-top:6px;text-align:{align};"">
  <div style=""color:{Muted};font-size:13px;text-transform:uppercase;letter-spacing:1px;margin-bottom:4px;"">{Enc(title)}</div>
  <div><strong>{name}</strong></div>
  <div style=""color:{Muted};"">{line}</div>
</div>";
    }
}
