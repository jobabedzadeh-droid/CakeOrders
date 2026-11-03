namespace CakeOrders.Domain.Entities;

public sealed class ProductionTaskItem
{
    public long Id { get; set; }

    public long ProductionTaskId { get; set; }
    public ProductionTask ProductionTask { get; set; } = null!;

    // Null for cake orders whose details are stored directly on Order.
    public long? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    // Snapshot values keep the production sheet stable if the catalog changes later.
    public string ProductName { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
