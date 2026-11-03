using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.Services;

public interface IOrderProductionRoutingService
{
    Task<bool> EnsureBirthdayCakeTaskAsync(
        Order order,
        string? actorUserId,
        DateTime now,
        CancellationToken cancellationToken);
}
