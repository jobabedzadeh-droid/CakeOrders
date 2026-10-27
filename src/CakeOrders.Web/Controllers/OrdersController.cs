using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

public sealed class OrdersController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return View(orders);
    }

    [HttpGet]
    public IActionResult Create() => View(new OrderCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.CalculatedTotal < 0)
            ModelState.AddModelError(nameof(model.Discount), "تخفیف نمی‌تواند از جمع هزینه‌ها بیشتر باشد.");
        if (model.DepositAmount > Math.Max(0, model.CalculatedTotal))
            ModelState.AddModelError(nameof(model.DepositAmount), "بیعانه نمی‌تواند از مبلغ نهایی بیشتر باشد.");
        if (!ModelState.IsValid)
            return View(model);

        var order = new Order
        {
            OrderNumber = $"CO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Channel = model.Channel,
            Status = model.Channel == OrderChannel.Online ? OrderStatus.PendingReview : OrderStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow,
            DeliveryAt = DateTime.SpecifyKind(model.DeliveryAt, DateTimeKind.Local).ToUniversalTime(),
            CustomerName = model.CustomerName?.Trim(),
            CustomerPhone = model.CustomerPhone?.Trim(),
            OrderType = model.OrderType,
            CakeType = model.CakeType,
            RequestedWeightKg = model.RequestedWeightKg,
            PricePerKg = model.PricePerKg,
            SecondaryPricePerKg = model.SecondaryPricePerKg,
            PrintType = model.PrintType,
            BackgroundColor = model.BackgroundColor?.Trim(),
            MoldType = model.MoldType,
            HasBananaFilling = model.HasBananaFilling,
            HasWalnutFilling = model.HasWalnutFilling,
            WritingLocation = model.WritingLocation,
            DesignNotes = model.DesignNotes?.Trim(),
            MacaronCost = model.MacaronCost,
            FlowerCost = model.FlowerCost,
            DummyCost = model.DummyCost,
            PrintingCost = model.PrintingCost,
            ShippingCost = model.ShippingCost,
            OtherCost = model.OtherCost,
            Discount = model.Discount,
            FinalAmount = model.CalculatedTotal,
            DepositAmount = model.DepositAmount,
            ReferrerCode = model.ReferrerCode?.Trim()
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = order.Status,
            NewStatus = order.Status,
            ChangedAtUtc = DateTime.UtcNow,
            Note = "ثبت اولیه سفارش"
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"سفارش {order.OrderNumber} ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }
}