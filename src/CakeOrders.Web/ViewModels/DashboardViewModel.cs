using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.ViewModels;

public sealed class DashboardViewModel
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ConfirmedOrders { get; set; }
    public int InProductionOrders { get; set; }
    public int ReadyForDeliveryOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int TodayDeliveries { get; set; }
    public int UpcomingDeliveries { get; set; }

    public decimal TotalSales { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal RemainingPayments { get; set; }

    public IReadOnlyList<Order> RecentOrders { get; set; }
        = Array.Empty<Order>();
}