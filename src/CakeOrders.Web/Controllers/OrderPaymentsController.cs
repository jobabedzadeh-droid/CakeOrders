using System.Data;
using System.Security.Claims;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels.Orders;
using CakeOrders.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class OrderPaymentsController(CakeOrdersDbContext db, IOrderProductionRoutingService productionRouting) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveCash(long orderId, decimal amount, PaymentPurpose purpose,
        string idempotencyKey, string? notes, string? receiptReference, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return BadRequest("کلید ثبت دریافت وجه معتبر نیست.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await db.Orders.FromSqlInterpolated(
            $"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) return NotFound();

        var existing = await db.OrderCashPayments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.OrderId != orderId) return Conflict("کلید ثبت برای سفارش دیگری استفاده شده است.");
            await transaction.CommitAsync(cancellationToken);
            TempData["Success"] = "این دریافت وجه قبلاً ثبت شده است.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected || order.RemainingAmount <= 0)
            return BadRequest("برای سفارش لغوشده یا ردشده، یا سفارشی که مانده ندارد، امکان دریافت وجه وجود ندارد.");
        if (!Enum.IsDefined(typeof(PaymentPurpose), purpose))
            return BadRequest("نوع دریافت وجه معتبر نیست.");

        var unresolved = await db.PaymentTransactions.AnyAsync(x => x.OrderId == orderId &&
            (x.Status == PaymentTransactionStatus.Created || x.Status == PaymentTransactionStatus.Processing ||
             x.Status == PaymentTransactionStatus.Unknown || x.Status == PaymentTransactionStatus.ReconciliationRequired),
            cancellationToken);
        if (unresolved) return Conflict("تراکنش کارت‌خوان باز یا نامشخص وجود دارد؛ ابتدا آن را تعیین تکلیف کنید.");

        var depositRemaining = Math.Max(0, order.RequestedDepositAmount - order.DepositAmount);
        if (purpose == PaymentPurpose.Deposit && depositRemaining <= 0)
            return BadRequest("بیعانه قبلاً کامل پرداخت شده است.");
        if (purpose == PaymentPurpose.Settlement && depositRemaining > 0)
            return BadRequest("تا پرداخت کامل بیعانه، تسویه مجاز نیست.");

        var limit = purpose == PaymentPurpose.Deposit
            ? Math.Min(depositRemaining, order.RemainingAmount) : order.RemainingAmount;
        if (amount <= 0 || amount > limit)
            return BadRequest("مبلغ باید بیشتر از صفر و حداکثر برابر مانده مجاز باشد.");
        if (decimal.Truncate(amount) != amount)
            return BadRequest("مبلغ نقدی باید عدد صحیح و برحسب ریال باشد.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();
        var now = DateTime.UtcNow;

        var cashPayment = new OrderCashPayment
        {
            OrderId = orderId, Amount = amount, Purpose = purpose,
            ReceivedByUserId = userId, ReceivedAtUtc = now,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ReceiptReference = string.IsNullOrWhiteSpace(receiptReference) ? null : receiptReference.Trim(),
            IdempotencyKey = idempotencyKey
        };
        db.OrderCashPayments.Add(cashPayment);
        await db.SaveChangesAsync(cancellationToken); // Generate payment ID before linking its receipt outbox row.
        order.PaidAmount += amount;
        if (purpose == PaymentPurpose.Deposit)
        {
            order.DepositAmount += amount;
            db.RahkaranReceiptOutbox.Add(new RahkaranReceiptOutbox
            {
                OrderId = order.Id,
                OrderCashPaymentId = cashPayment.Id,
                Source = RahkaranReceiptSource.Cash,
                Amount = amount,
                Status = RahkaranReceiptSyncStatus.Pending,
                CreatedAtUtc = now
            });
        }

        if (purpose == PaymentPurpose.Deposit && order.Status == OrderStatus.PendingReview &&
            order.RequestedDepositAmount > 0 && order.DepositAmount >= order.RequestedDepositAmount)
        {
            if (order.Kind == OrderKind.BirthdayCake &&
                !await productionRouting.EnsureBirthdayCakeTaskAsync(order, userId, now, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["Error"] = "کارگاه فعال کیک‌سازی پیدا نشد؛ دریافت وجه ثبت نشد تا سفارش بدون وظیفه تولید تأیید نشود.";
                return RedirectToAction("Details", "Orders", new { id = orderId });
            }

            var previous = order.Status;
            order.Status = OrderStatus.Confirmed;
            order.UpdatedByUserId = userId;

            await RahkaranSalesRequestOutboxEnqueuer.EnqueueIfMissingAsync(
                db, order.Id, now, cancellationToken);
            order.StatusHistory.Add(new OrderStatusHistory
            {
                PreviousStatus = previous, NewStatus = OrderStatus.Confirmed,
                ChangedAtUtc = now, ChangedByUserId = null,
                Note = "تأیید خودکار سیستمی: بیعانه درخواستی به‌طور کامل به‌صورت نقدی دریافت و توسط اپراتور ثبت شد."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        TempData["Success"] = "دریافت نقدی با موفقیت ثبت شد.";
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    [HttpGet]
    public async Task<IActionResult> Start(long orderId, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected || order.RemainingAmount <= 0)
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

        var depositRemaining = Math.Max(0, order.RequestedDepositAmount - order.DepositAmount);
        var defaultPurpose = depositRemaining > 0 ? PaymentPurpose.Deposit : PaymentPurpose.Settlement;
        var model = new OrderPaymentStartViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            RemainingAmount = order.RemainingAmount,
            Purpose = defaultPurpose,
            Amount = defaultPurpose == PaymentPurpose.Deposit ? depositRemaining : order.RemainingAmount,
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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // قفل ردیف سفارش تا دو درخواست هم‌زمان نتوانند برای یک مانده، پرداخت موازی بسازند.
        var order = await db.Orders
            .FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {model.OrderId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected || order.RemainingAmount <= 0)
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
        if (model.Purpose == PaymentPurpose.Settlement && depositRemaining > 0)
            ModelState.AddModelError(nameof(model.Purpose), "تا زمان پرداخت کامل بیعانه، امکان تسویه سفارش وجود ندارد.");

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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

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

        if (hasUnresolved || order.Status is OrderStatus.Cancelled or OrderStatus.Rejected || previous.Amount > available)
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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

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

    private async Task<bool> IsWorkshopMemberAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return false;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive && x.WorkshopId != null,
                cancellationToken);
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