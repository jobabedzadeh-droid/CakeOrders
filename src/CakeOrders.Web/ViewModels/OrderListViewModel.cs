using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.ViewModels;

public sealed class OrderListViewModel
{
    public string? Search { get; set; }

    public OrderStatus? Status { get; set; }

    public OrderChannel? Channel { get; set; }

    public DateTime? DeliveryFrom { get; set; }

    public DateTime? DeliveryTo { get; set; }

    public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }

    public int TotalPages =>
        (int)Math.Ceiling(TotalCount / (double)PageSize);
}