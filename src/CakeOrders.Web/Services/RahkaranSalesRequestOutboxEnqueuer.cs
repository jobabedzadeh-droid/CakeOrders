using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Services;

/// <summary>
/// Adds the Rahkaran sales-request work item to the current EF transaction.
/// This method performs no network calls.
/// </summary>
public static class RahkaranSalesRequestOutboxEnqueuer
{
    public static async Task EnqueueIfMissingAsync(
        CakeOrdersDbContext db,
        long orderId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var exists = await db.RahkaranSalesRequestOutbox
            .AnyAsync(x => x.OrderId == orderId, cancellationToken);

        if (exists)
            return;

        db.RahkaranSalesRequestOutbox.Add(new RahkaranSalesRequestOutbox
        {
            OrderId = orderId,
            Status = RahkaranSalesRequestSyncStatus.Pending,
            CreatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            NextAttemptAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)
        });
    }
}
