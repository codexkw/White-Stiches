using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Orders + draft orders back office (AD-ORD-01..05, AD-ORD-08).</summary>
[Route("orders")]
public class OrdersController(
    IOrderAdminService orderAdmin,
    IOrderService orders,
    IInvoicePdfService invoicePdf,
    ISettingsService settings,
    IAuditService audit) : Controller
{
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? CurrentUserName => User.Identity?.Name;

    // ------------------------------------------------------------- list

    [HttpGet("")]
    public async Task<IActionResult> Index(OrderStatus? status, PaymentStatus? paymentStatus,
        OrderChannel? channel, string? search, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Orders";
        ViewData["Nav"] = "orders";

        var result = await orderAdmin.GetOrdersAdminAsync(status, paymentStatus, channel, search,
            isDraft: false, page: page, ct: ct);

        return View(new OrderListViewModel
        {
            Orders = result,
            Status = status,
            PaymentStatus = paymentStatus,
            Channel = channel,
            Search = search
        });
    }

    // ------------------------------------------------------------- detail

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
    {
        var order = await orderAdmin.GetDetailAsync(id, ct);
        if (order is null)
        {
            TempData["Err"] = $"Order #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (order.IsDraft)
        {
            return RedirectToAction(nameof(EditDraft), new { id });
        }

        ViewData["Title"] = $"Order {order.OrderNumber}";
        ViewData["Nav"] = "orders";

        return View(new OrderDetailViewModel
        {
            Order = order,
            TotalPaid = order.Payments.Where(p => p.Status == TransactionStatus.Captured).Sum(p => p.Amount),
            TotalRefunded = order.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount)
        });
    }

    // ------------------------------------------------------------- invoice (AD-ORD download)

    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct = default)
    {
        var order = await orderAdmin.GetDetailAsync(id, ct);
        if (order is null)
        {
            TempData["Err"] = $"Order #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }
        if (order.IsDraft)
        {
            TempData["Err"] = "Invoices are not available for draft orders.";
            return RedirectToAction(nameof(EditDraft), new { id });
        }

        var nameEn = await settings.GetAsync(SettingKeys.StoreNameEn, ct);
        var nameAr = await settings.GetAsync(SettingKeys.StoreNameAr, ct);
        var storeName = !string.IsNullOrWhiteSpace(nameEn) ? nameEn!
            : !string.IsNullOrWhiteSpace(nameAr) ? nameAr! : "White Stitches";

        var branding = new InvoiceBranding(
            StoreName: storeName,
            LogoPath: null,
            ContactEmail: await settings.GetAsync(SettingKeys.ContactEmail, ct),
            ContactPhone: await settings.GetAsync(SettingKeys.ContactPhone, ct),
            TotalPaid: order.Payments.Where(p => p.Status == TransactionStatus.Captured).Sum(p => p.Amount),
            TotalRefunded: order.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount));

        var bytes = invoicePdf.Build(order, branding);
        return File(bytes, "application/pdf", $"invoice-{order.OrderNumber}.pdf");
    }

    // ------------------------------------------------------------- delivery note (no pricing, for the courier)

    [HttpGet("{id:int}/delivery-note")]
    public async Task<IActionResult> DeliveryNote(int id, CancellationToken ct = default)
    {
        var order = await orderAdmin.GetDetailAsync(id, ct);
        if (order is null)
        {
            TempData["Err"] = $"Order #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }
        if (order.IsDraft)
        {
            TempData["Err"] = "Delivery notes are not available for draft orders.";
            return RedirectToAction(nameof(EditDraft), new { id });
        }

        var nameEn = await settings.GetAsync(SettingKeys.StoreNameEn, ct);
        var nameAr = await settings.GetAsync(SettingKeys.StoreNameAr, ct);
        var storeName = !string.IsNullOrWhiteSpace(nameEn) ? nameEn!
            : !string.IsNullOrWhiteSpace(nameAr) ? nameAr! : "White Stitches";

        // Pricing fields are unused by the delivery-note render, but the record requires them.
        var branding = new InvoiceBranding(
            StoreName: storeName,
            LogoPath: null,
            ContactEmail: await settings.GetAsync(SettingKeys.ContactEmail, ct),
            ContactPhone: await settings.GetAsync(SettingKeys.ContactPhone, ct),
            TotalPaid: 0m,
            TotalRefunded: 0m);

        var bytes = invoicePdf.BuildDeliveryNote(order, branding);
        return File(bytes, "application/pdf", $"delivery-note-{order.OrderNumber}.pdf");
    }

    // ------------------------------------------------------------- order actions

    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, CancellationToken ct = default)
    {
        try
        {
            var order = await orders.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException($"Order {id} not found.");
            var before = order.Status;

            await orders.UpdateStatusAsync(id, status, CurrentUserId, ct);
            await audit.LogAsync("order.status", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { Status = before.ToString() }, new { Status = status.ToString() }, ct: ct);

            TempData["Ok"] = $"Order {order.OrderNumber} status set to {status}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/mark-paid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id, decimal? amount, string? reference, CancellationToken ct = default)
    {
        try
        {
            var payment = await orderAdmin.MarkPaidAsync(id, amount, reference, CurrentUserId, CurrentUserName, ct);
            await audit.LogAsync("order.mark-paid", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                null, new { payment.Amount, Reference = reference }, ct: ct);

            TempData["Ok"] = $"Manual payment of {payment.Amount.ToString("0.000")} KWD recorded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/fulfill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fulfill(int id, string? carrier, string? trackingNumber, string? trackingUrl,
        CancellationToken ct = default)
    {
        try
        {
            var shipment = await orderAdmin.FulfillAsync(id, carrier, trackingNumber, trackingUrl,
                CurrentUserId, CurrentUserName, ct);
            await audit.LogAsync("order.fulfill", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                null, new { shipment.Carrier, shipment.AwbNumber, shipment.TrackingUrl }, ct: ct);

            TempData["Ok"] = "Shipment created and order marked fulfilled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason, bool restock = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Err"] = "A cancellation reason is required.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        try
        {
            var order = await orders.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException($"Order {id} not found.");
            var before = order.Status;

            await orders.CancelAsync(id, reason.Trim(), restock, CurrentUserId, ct);
            await audit.LogAsync("order.cancel", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { Status = before.ToString() },
                new { Status = OrderStatus.Cancelled.ToString(), Reason = reason.Trim(), Restock = restock }, ct: ct);

            TempData["Ok"] = $"Order {order.OrderNumber} cancelled" + (restock ? " and stock returned." : ".");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/refund")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefundOrder(int id, decimal amount, string? reason, CancellationToken ct = default)
    {
        try
        {
            var refund = await orderAdmin.RefundAsync(id, amount, reason, CurrentUserId, CurrentUserName, ct);
            await audit.LogAsync("order.refund", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                null, new { refund.Amount, refund.Reason }, ct: ct);

            TempData["Ok"] = $"Refund of {refund.Amount.ToString("0.000")} KWD recorded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/note")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNote(int id, string? internalNote, CancellationToken ct = default)
    {
        try
        {
            var order = await orderAdmin.GetDetailAsync(id, ct)
                ?? throw new InvalidOperationException($"Order {id} not found.");
            var before = order.InternalNote;

            await orderAdmin.SaveInternalNoteAsync(id, internalNote, ct);
            await audit.LogAsync("order.note", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { InternalNote = before }, new { InternalNote = internalNote }, ct: ct);

            TempData["Ok"] = "Internal note saved.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Err"] = "Comment text is required.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        await orders.AddEventAsync(id, "staff.comment", text.Trim(), CurrentUserId, CurrentUserName, ct);
        await audit.LogAsync("order.comment", CurrentUserId, CurrentUserName, "Order", id.ToString(),
            null, new { Text = text.Trim() }, ct: ct);

        TempData["Ok"] = "Comment added to the timeline.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ------------------------------------------------------------- drafts (AD-ORD-08)

    [HttpGet("drafts")]
    public async Task<IActionResult> Drafts(OrderChannel? channel, string? search, int page = 1,
        CancellationToken ct = default)
    {
        ViewData["Title"] = "Draft orders";
        ViewData["Nav"] = "orders";

        var result = await orderAdmin.GetOrdersAdminAsync(null, null, channel, search,
            isDraft: true, page: page, ct: ct);

        return View(new DraftListViewModel { Drafts = result, Channel = channel, Search = search });
    }

    [HttpGet("drafts/new")]
    public IActionResult NewDraft()
    {
        ViewData["Title"] = "New draft order";
        ViewData["Nav"] = "orders";
        return View();
    }

    [HttpPost("drafts/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(string? email, string? phone, string? firstName, string? lastName,
        OrderChannel channel = OrderChannel.Instagram, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            TempData["Err"] = "Provide an email address or phone number for the customer.";
            return RedirectToAction(nameof(NewDraft));
        }

        var draft = await orderAdmin.CreateDraftAsync(email ?? string.Empty, phone ?? string.Empty,
            firstName ?? string.Empty, lastName ?? string.Empty, channel, CurrentUserId, CurrentUserName, ct);

        await audit.LogAsync("order.draft.create", CurrentUserId, CurrentUserName, "Order", draft.Id.ToString(),
            null, new { draft.OrderNumber, Channel = channel.ToString(), Email = email, Phone = phone }, ct: ct);

        TempData["Ok"] = $"Draft {draft.OrderNumber} created.";
        return RedirectToAction(nameof(EditDraft), new { id = draft.Id });
    }

    [HttpGet("drafts/{id:int}")]
    public async Task<IActionResult> EditDraft(int id, string? productSearch, CancellationToken ct = default)
    {
        var order = await orderAdmin.GetDetailAsync(id, ct);
        if (order is null)
        {
            TempData["Err"] = $"Draft #{id} was not found.";
            return RedirectToAction(nameof(Drafts));
        }

        if (!order.IsDraft)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        ViewData["Title"] = $"Draft {order.OrderNumber}";
        ViewData["Nav"] = "orders";

        IReadOnlyList<Product> results = string.IsNullOrWhiteSpace(productSearch)
            ? []
            : await orderAdmin.SearchProductsForDraftAsync(productSearch, ct: ct);

        return View(new DraftEditViewModel
        {
            Order = order,
            ProductSearch = productSearch,
            ProductResults = results
        });
    }

    [HttpPost("drafts/{id:int}/items/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDraftItem(int id, int variantId, int quantity = 1,
        string? productSearch = null, CancellationToken ct = default)
    {
        try
        {
            await orderAdmin.AddDraftItemAsync(id, variantId, quantity, ct);
            await audit.LogAsync("order.draft.item-add", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                null, new { VariantId = variantId, Quantity = quantity }, ct: ct);

            TempData["Ok"] = "Item added to the draft.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(EditDraft), new { id, productSearch });
    }

    [HttpPost("drafts/{id:int}/items/{itemId:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDraftItem(int id, int itemId, CancellationToken ct = default)
    {
        try
        {
            await orderAdmin.RemoveDraftItemAsync(id, itemId, ct);
            await audit.LogAsync("order.draft.item-remove", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { ItemId = itemId }, null, ct: ct);

            TempData["Ok"] = "Item removed from the draft.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(EditDraft), new { id });
    }

    [HttpPost("drafts/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(int id, DraftUpdateForm form, CancellationToken ct = default)
    {
        try
        {
            await orderAdmin.UpdateDraftAsync(id, new DraftOrderUpdate(
                form.ShippingAmount, form.DiscountAmount, form.CustomerNote,
                form.Email, form.Phone,
                form.ShipFirstName, form.ShipLastName, form.ShipPhone,
                form.ShipGovernorate, form.ShipArea, form.ShipBlock, form.ShipStreet,
                form.ShipBuilding, form.ShipFloor, form.ShipApartment, form.ShipDirections), ct);

            await audit.LogAsync("order.draft.update", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                null, new { form.ShippingAmount, form.DiscountAmount }, ct: ct);

            TempData["Ok"] = "Draft saved.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(EditDraft), new { id });
    }

    [HttpPost("drafts/{id:int}/convert")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertDraft(int id, CancellationToken ct = default)
    {
        try
        {
            await orderAdmin.ConvertDraftAsync(id, CurrentUserId, CurrentUserName, ct);
            await audit.LogAsync("order.draft.convert", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { IsDraft = true }, new { IsDraft = false, Status = OrderStatus.Placed.ToString() }, ct: ct);

            TempData["Ok"] = "Draft converted to a live order.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(EditDraft), new { id });
        }
    }

    [HttpPost("drafts/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken ct = default)
    {
        try
        {
            var order = await orderAdmin.GetDetailAsync(id, ct)
                ?? throw new InvalidOperationException($"Draft #{id} was not found.");
            var number = order.OrderNumber;

            await orderAdmin.DeleteDraftAsync(id, ct);
            await audit.LogAsync("order.draft.delete", CurrentUserId, CurrentUserName, "Order", id.ToString(),
                new { OrderNumber = number }, null, ct: ct);

            TempData["Ok"] = $"Draft {number} deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Err"] = ex.Message;
        }

        return RedirectToAction(nameof(Drafts));
    }
}
