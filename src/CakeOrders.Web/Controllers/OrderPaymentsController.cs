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
            Amount = Math.Max(0, order.RequestedDepositAmount - order.DepositAmount),
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

        var depositRemaining = Math.Max(0, order.RequestedDepositAmount - order.DepositAmount);
        var amount = model.Purpose == PaymentPurpose.Settlement
            ? order.RemainingAmount
            : model.Amount;

        if (model.Purpose == PaymentPurpose.Deposit && depositRemaining <= 0)
            ModelState.AddModelError(nameof(model.Purpose), "بیعانه درخواستی قبلاً به‌طور کامل پرداخت شده است.");

        var purposeLimit = model.Purpose == PaymentPurpose.Deposit
            ? depositRemaining
            : order.RemainingAmount;

        // For full settlement, the server determines the exact remaining balance.
        // The posted amount is ignored.
        if (model.Purpose == PaymentPurpose.Settlement)
            ModelState.Remove(nameof(model.Amount));

        if (amount <= 0 || amount > purposeLimit)
            ModelState.AddModelError(nameof(model.Amount), "مبلغ پرداخت باید بیشتر از صفر و حداکثر برابر مانده مجاز این نوع پرداخت باشد.");

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

        return RedirectToAction(nameof(Wait), new { paymentId = payment.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(long paymentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var previous = await db.PaymentTransactions
            .FromSqlInterpolated($"SELECT * FROM [PaymentTransactions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {paymentId}")
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is null)
            return NotFound();

        if (previous.Status is not (PaymentTransactionStatus.Failed or PaymentTransactionStatus.Cancelled))
        {
            TempData["Error"] = "ارسال مجدد فقط برای تراکنش ناموفق یا لغوشده مجاز است.";
            return RedirectToAction(nameof(Wait), new { paymentId });
        }

        var order = await db.Orders
            .FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {previous.OrderId}")
            .SingleAsync(cancellationToken);

        var hasUnresolved = await db.PaymentTransactions.AnyAsync(x =>
            x.OrderId == order.Id &&
            (x.Status == PaymentTransactionStatus.Created ||
             x.Status == PaymentTransactionStatus.Processing ||
             x.Status == PaymentTransactionStatus.Unknown ||
             x.Status == PaymentTransactionStatus.ReconciliationRequired), cancellationToken);

        var available = previous.Purpose == PaymentPurpose.Deposit
            ? Math.Max(0, order.RequestedDepositAmount - order.DepositAmount)
            : order.RemainingAmount;

        if (hasUnresolved || order.Status == OrderStatus.Cancelled || previous.Amount > available)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Error"] = "به دلیل وجود تراکنش باز یا تغییر مانده سفارش، امکان ارسال مجدد وجود ندارد.";
            return RedirectToAction(nameof(Wait), new { paymentId });
        }

        var register = await db.CashRegisters
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.Id == previous.CashRegisterId, cancellationToken);

        if (register is null || !register.IsActive || !register.Branch.IsActive ||
            register.PosTerminal is null || !register.PosTerminal.IsActive ||
            string.IsNullOrWhiteSpace(register.BridgeInstallationId) ||
            string.IsNullOrWhiteSpace(register.BridgeSecretHash))
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Error"] = "صندوق قبلی فعال نیست؛ از صفحه درخواست پرداخت، صندوق دیگری انتخاب کنید.";
            return RedirectToAction(nameof(Wait), new { paymentId });
        }

        var now = DateTime.UtcNow;
        var retry = new PaymentTransaction
        {
            OrderId = order.Id,
            CashRegisterId = register.Id,
            PosTerminalId = register.PosTerminal.Id,
            Purpose = previous.Purpose,
            Amount = previous.Amount,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            MerchantReference = Guid.NewGuid().ToString("N"),
            Status = PaymentTransactionStatus.Created,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedAtUtc = now
        };

        db.PaymentTransactions.Add(retry);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Wait), new { paymentId = retry.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Wait(long paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return NotFound();

        ViewBag.StatusUrl = Url.Action(nameof(Status), new { paymentId });
        ViewBag.OrderDetailsUrl = Url.Action("Details", "Orders", new { id = payment.OrderId });
        return View(payment);
    }

    [HttpGet]
    public async Task<IActionResult> Status(long paymentId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        var payment = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return NotFound();

        var isFinal = payment.Status is
            PaymentTransactionStatus.Succeeded or
            PaymentTransactionStatus.Failed or
            PaymentTransactionStatus.Cancelled or
            PaymentTransactionStatus.ReconciliationRequired;

        var message = payment.Status switch
        {
            PaymentTransactionStatus.Created => "درخواست ثبت شده و در انتظار دریافت توسط برنامه صندوق است.",
            PaymentTransactionStatus.Processing => "درخواست به کارت‌خوان ارسال شده است؛ منتظر پاسخ مشتری و دستگاه هستیم.",
            PaymentTransactionStatus.Succeeded => "پرداخت با موفقیت تأیید شد.",
            PaymentTransactionStatus.Failed => "پرداخت ناموفق بود.",
            PaymentTransactionStatus.Cancelled => "تراکنش توسط کاربر کارت‌خوان لغو شد.",
            PaymentTransactionStatus.Unknown => "هنوز نتیجه قطعی دریافت نشده است. تا روشن شدن نتیجه، درخواست پرداخت دیگری ثبت نکنید.",
            PaymentTransactionStatus.ReconciliationRequired => "این تراکنش نیازمند بررسی و تطبیق دستی است.",
            _ => "وضعیت تراکنش قابل تشخیص نیست."
        };

        return Ok(new
        {
            paymentId = payment.Id,
            status = payment.Status.ToString(),
            message,
            responseMessage = payment.ResponseMessage,
            amount = payment.Amount,
            purpose = payment.Purpose.ToString(),
            rrn = payment.Rrn,
            traceNumber = payment.TraceNumber,
            completedAtUtc = payment.CompletedAtUtc,
            isFinal
        });
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