using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.ViewModels.Orders;

public sealed class OrderItemCreateViewModel
{
    public long ProductId { get; set; }

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public long WorkshopId { get; set; }

    // فقط برای نمایش در فرم
    public string? ProductName { get; set; }

    public ProductUnit Unit { get; set; }
}