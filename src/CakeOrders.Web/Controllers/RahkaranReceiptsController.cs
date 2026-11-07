using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.Services;
using CakeOrders.Web.ViewModels.Integrations.Rahkaran;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RahkaranReceiptsController(
    CakeOrdersDbContext db,
    RahkaranReceiptOutboxWorker receiptWorker) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await db.RahkaranReceiptOutbox
            .AsNoTracking()
            .Include(x => x.Order)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .Select(x => new RahkaranReceiptOutboxRowViewModel
            {
                Id = x.Id,
                OrderId = x.OrderId,
                OrderNumber = x.Order.OrderNumber,
                CustomerName = x.Order.CustomerName,
                CustomerPhone = x.Order.CustomerPhone,
                Source = x.Source,
                Status = x.Status,
                Amount = x.Amount,
                CreatedAtUtc = x.CreatedAtUtc,
                RemoteReceiptNumber = x.RemoteReceiptNumber,
                LastError = x.LastError
            })
            .ToListAsync(cancellationToken);

        return View(new RahkaranReceiptOutboxListViewModel { Items = items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest();

        var status = await receiptWorker.DispatchSpecificAsync(id, cancellationToken);

        if (status is null)
        {
            TempData["Error"] = "این رسید دیگر در وضعیت «در انتظار ارسال» نیست یا قبلاً توسط پردازش دیگری دریافت شده است. فهرست را تازه‌سازی کنید.";
            return RedirectToAction(nameof(Index));
        }

        if (status == RahkaranReceiptSyncStatus.Succeeded)
        {
            TempData["Success"] = "رسید انتخاب‌شده با موفقیت در راهکاران ثبت شد.";
        }
        else
        {
            TempData["Error"] = "ارسال رسید به نتیجه قطعی موفقیت نرسید و برای جلوگیری از ثبت تکراری به وضعیت «نیازمند بررسی» منتقل شد. قبل از هر اقدام بعدی، راهکاران را بررسی کنید.";
        }

        return RedirectToAction(nameof(Index));
    }
}
