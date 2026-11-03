using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Services;

public sealed class UserNotificationService(
    CakeOrdersDbContext db,
    UserManager<ApplicationUser> userManager) : IUserNotificationService
{
    public async Task NotifyWorkshopTaskRejectedAsync(
        long orderId,
        string orderNumber,
        string workshopName,
        string rejectionReason,
        string? operatorUserId,
        CancellationToken cancellationToken = default)
    {
        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");

        var recipientIds = adminUsers
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(operatorUserId))
        {
            var operatorUser = await db.Users
                .AsNoTracking()
                .Where(x => x.Id == operatorUserId && x.IsActive)
                .Select(x => new { x.Id })
                .SingleOrDefaultAsync(cancellationToken);

            if (operatorUser is not null)
                recipientIds.Add(operatorUser.Id);
        }

        if (recipientIds.Count == 0)
            return;

        var title = $"رد وظیفه تولید سفارش {orderNumber}";
        var message =
            $"کارگاه «{workshopName}» وظیفه تولید سفارش {orderNumber} را رد کرد. " +
            $"دلیل اعلام‌شده: {rejectionReason.Trim()}";

        var now = DateTime.UtcNow;
        var notifications = recipientIds.Select(userId => new UserNotification
        {
            RecipientUserId = userId,
            Title = title,
            Message = message,
            Link = $"/Orders/Details/{orderId}",
            CreatedAtUtc = now
        });

        await db.UserNotifications.AddRangeAsync(notifications, cancellationToken);
    }
}
