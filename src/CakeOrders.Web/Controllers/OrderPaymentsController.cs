using System.Data;
using System.Security.Claims;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class OrderPaymentsController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Start(long orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Status == OrderStatus.Cancelled || order.RemainingAmount <= 0)
        {
            TempData["Error"] = "این سفارش امکان دریافت وجه بیشتری ندارد.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        var registers = await GetAvailableRegisters(cancellationToken);

        if (registers.Count == 0)
        {
            TempData["Error"] = "هیچ صندوق فعال و دارای دسترسی SadadBridge پیدا نشد. ابتدا صندوق، کارت‌خوان و دسترسی بریج را بررسی کنید.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        var model = new OrderPaymentStartViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            RemainingAmount = order.RemainingAmount,
            Amount = order.RemainingAmount,
            CashRegisterId = registers[0].Value is string value && long.TryParse(value, out var id) ? id : 0,
            CashRegisters = registers
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(
        OrderPaymentStartViewModel model,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // قفل ردیف سفارش تا دو درخواست هم‌زمان نتوانند برای یک مانده، پرداخت موازی بسازند.
        var order = await db.Orders
            .FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {model.OrderId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Status == OrderStatus.Cancelled || order.RemainingAmount <= 0)
        {
            TempData["Error"] = "این سفارش امکان دریافت وجه بیشتری ندارد.";
            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }

        var register = await db.CashRegisters
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.Id == model.CashRegisterId, cancellationToken);

        if (register is null ||
            !register.IsActive ||
            !register.Branch.IsActive ||
            register.PosTerminal is null ||
            !register.PosTerminal.IsActive ||
            string.IsNullOrWhiteSpace(register.BridgeInstallationId) ||
            string.IsNullOrWhiteSpace(register.BridgeSecretHash))
        {
            ModelState.AddModelError(
                nameof(model.CashRegisterId),
                "صندوق انتخاب‌شده فعال نیست یا دسترسی معتبر SadadBridge ندارد.");
        }

        if (!Enum.IsDefined(typeof(PaymentPurpose), model.Purpose))
            ModelState.AddModelError(nameof(model.Purpose), "نوع پرداخت معتبر نیست.");

        var hasUnresolvedPayment = await db.PaymentTransactions.AnyAsync(
            x => x.OrderId == order.Id &&
                 (x.Status == PaymentTransactionStatus.Created ||
                  x.Status == PaymentTransactionStatus.Processing ||
                  x.Status == PaymentTransactionStatus.Unknown ||
                  x.Status == PaymentTransactionStatus.ReconciliationRequired),
            cancellationToken);

        if (hasUnresolvedPayment)
        {
            ModelState.AddModelError(
                string.Empty,
                "برای این سفارش یک تراکنش در انتظار یا با نتیجه نامشخص وجود دارد. ابتدا آن را تعیین تکلیف کنید.");
        }

        var amount = model.Purpose == PaymentPurpose.Settlement
            ? order.RemainingAmount
            : model.Amount;

        // For full settlement, the server determines the exact remaining balance.
        // The posted amount is ignored.
        if (model.Purpose == PaymentPurpose.Settlement)
            ModelState.Remove(nameof(model.Amount));

        if (amount <= 0 || amount > order.RemainingAmount)
            ModelState.AddModelError(nameof(model.Amount), "مبلغ پرداخت باید بیشتر از صفر و حداکثر برابر مانده سفارش باشد.");

        if (decimal.Truncate(amount) != amount)
            ModelState.AddModelError(nameof(model.Amount), "مبلغ کارت‌خوان باید عدد صحیح و برحسب ریال باشد.");

        if (!ModelState.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            var registers = await GetAvailableRegisters(cancellationToken);
            model.CashRegisters = registers;
            model.OrderNumber = order.OrderNumber;
            model.RemainingAmount = order.RemainingAmount;
            return View(model);
        }

        var now = DateTime.UtcNow;
        var payment = new PaymentTransaction
        {
            OrderId = order.Id,
            CashRegisterId = register!.Id,
            PosTerminalId = register.PosTerminal!.Id,
            Purpose = model.Purpose,
            Amount = amount,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            MerchantReference = Guid.NewGuid().ToString("N"),
            Status = PaymentTransactionStatus.Created,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedAtUtc = now
        };

        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = $"درخواست پرداخت {amount:N0} ریال ثبت شد و در صف صندوق قرار گرفت.";
        return RedirectToAction("Details", "Orders", new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> History(long orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound();

        var payments = await db.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        ViewBag.OrderId = order.Id;
        ViewBag.OrderNumber = order.OrderNumber;

        return View(payments);
    }

    private async Task<List<SelectListItem>> GetAvailableRegisters(
        CancellationToken cancellationToken)
    {
        return await db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .Where(x =>
                x.IsActive &&
                x.Branch.IsActive &&
                x.PosTerminal != null &&
                x.PosTerminal.IsActive &&
                x.BridgeInstallationId != null &&
                x.BridgeSecretHash != null)
            .OrderBy(x => x.Branch.Name)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Branch.Name + " / " + x.Name + " / " + x.PosTerminal!.Name
            })
            .ToListAsync(cancellationToken);
    }
}