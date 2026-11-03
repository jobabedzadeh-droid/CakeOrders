namespace CakeOrders.Domain.Entities;

public sealed class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // اطلاعات محصول در زمان ثبت سفارش
    public string ProductName { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }

    // کارگاه انتخاب‌شده برای همین سفارش
    public long WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}