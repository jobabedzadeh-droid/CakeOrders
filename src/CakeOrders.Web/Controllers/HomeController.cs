using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize]
public sealed class HomeController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(AdminDashboard));

        if (User.IsInRole("User"))
            return RedirectToAction(nameof(UserDashboard));

        return RedirectToAction(nameof(AccessDenied));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> AdminDashboard()
    {
        var model = await BuildDashboardAsync();
        return View(model);
    }

    [Authorize(Roles = "User")]
    [HttpGet]
    public async Task<IActionResult> UserDashboard()
    {
        var model = await BuildDashboardAsync();
        return View(model);
    }

    private async Task<DashboardViewModel> BuildDashboardAsync()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);

        var todayUtc = today.ToUniversalTime();
        var tomorrowUtc = tomorrow.ToUniversalTime();
        var nextWeekUtc = nextWeek.ToUniversalTime();
        var nowUtc = DateTime.UtcNow;

        var orders = db.Orders.AsNoTracking();

        var model = new DashboardViewModel
        {
            TotalOrders = await orders.CountAsync(),

            PendingOrders = await orders.CountAsync(
                x => x.Status == OrderStatus.PendingReview),

            ConfirmedOrders = await orders.CountAsync(
                x => x.Status == OrderStatus.Confirmed),

            InProductionOrders = await orders.CountAsync(
                x => x.Status == OrderStatus.InProduction),

            ReadyForDeliveryOrders = await orders.CountAsync(
                x => x.Status == OrderStatus.ReadyForDelivery),

            DeliveredOrders = await orders.CountAsync(
                x => x.Status == OrderStatus.Delivered),

            TodayDeliveries = await orders.CountAsync(
                x => x.DeliveryAt >= todayUtc &&
                    x.DeliveryAt < tomorrowUtc),

            UpcomingDeliveries = await orders.CountAsync(
                x => x.DeliveryAt >= nowUtc &&
                    x.DeliveryAt < nextWeekUtc),

            TotalSales = await orders
                .Where(x => x.Status != OrderStatus.Cancelled &&
                            x.Status != OrderStatus.Rejected)
                .SumAsync(x => (decimal?)x.FinalAmount) ?? 0,

            TotalDeposits = await orders
                .Where(x => x.Status != OrderStatus.Cancelled &&
                            x.Status != OrderStatus.Rejected)
                .SumAsync(x => (decimal?)x.DepositAmount) ?? 0,

            RemainingPayments = await orders
                .Where(x => x.Status != OrderStatus.Cancelled &&
                    x.Status != OrderStatus.Rejected)
                .SumAsync(x => (decimal?)(
                    x.FinalAmount > x.DepositAmount
                        ? x.FinalAmount - x.DepositAmount
                        : 0m)) ?? 0,

            RecentOrders = await orders
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(8)
                .ToListAsync()
        };

        return model;
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error()
    {
        return View("Index");
    }
}