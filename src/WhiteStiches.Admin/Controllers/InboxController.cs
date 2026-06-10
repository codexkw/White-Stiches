using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Contact-form inbox: list, read (marks read on view), mailto reply (AD-CNT). Routes under /inbox.</summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.MarketingManager},{AppRoles.ContentEditor},{AppRoles.CustomerService}")]
public class InboxController(IContentService content, IAuditService audit) : Controller
{
    [HttpGet("inbox")]
    public async Task<IActionResult> Index(bool unreadOnly = true, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Inbox";
        ViewData["Nav"] = "inbox";

        var messages = await content.GetContactMessagesAsync(unreadOnly, page, 25, ct);
        return View(new InboxListViewModel { Messages = messages, UnreadOnly = unreadOnly });
    }

    [HttpGet("inbox/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var message = await content.GetContactMessageAsync(id, ct);
        if (message is null)
        {
            TempData["Err"] = "Message not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!message.IsRead)
        {
            await content.MarkContactMessageReadAsync(id, ct);

            await audit.LogAsync("content.inbox.message.read",
                CurrentUserId(), User.Identity?.Name,
                nameof(ContactMessage), id.ToString(),
                new { IsRead = false }, new { IsRead = true }, ct: ct);

            message.IsRead = true;
        }

        ViewData["Title"] = $"Message — {message.Name}";
        ViewData["Nav"] = "inbox";

        return View(message);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
