using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.Services;

public interface IOrderProductionRoutingService
{
    Task<bool> EnsureBirthdayCakeTaskAsync(
        Order order,
        string? actorUserId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<bool> TryRouteBirthdayCakeAsync(
        Order order,
        string? actorUserId,
        DateTime now,
        CancellationToken cancellationToken);

    void MarkDeliveredIfSettled(Order order, string? actorUserId, DateTime now);

    void ChangeOrderStatus(
        Order order,
        OrderStatus newStatus,
        string? actorUserId,
        DateTime now,
        string note);
}
