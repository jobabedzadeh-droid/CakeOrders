namespace CakeOrders.Domain.Entities;

public sealed class OrderStatusHistory
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ChangedByUserId { get; set; }
    public string? Note { get; set; }
}
