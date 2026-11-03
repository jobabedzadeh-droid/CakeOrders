using System.Security.Claims;
using CakeOrders.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
[Route("[controller]/[action]")]
public sealed class NotificationsController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var notifications = await db.UserNotifications
            .AsNoTracking()
            .Where(x => x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var notification = await db.UserNotifications
            .FirstOrDefaultAsync(
                x => x.Id == id && x.RecipientUserId == userId,
                cancellationToken);

        if (notification is null)
            return NotFound();

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(notification.Link) &&
            Url.IsLocalUrl(notification.Link))
            return LocalRedirect(notification.Link);

        return RedirectToAction(nameof(Index));
    }
}
