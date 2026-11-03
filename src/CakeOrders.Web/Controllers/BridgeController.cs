using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[ApiController]
[Route("api/bridge")]
[Authorize(Policy = BridgeAuthenticationDefaults.Policy)]
public sealed class BridgeController(CakeOrdersDbContext db) : ControllerBase
{
    [HttpGet("terminals/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentTerminal(CancellationToken cancellationToken)
    {
        DisableCaching();

        var identity = GetBridgeIdentity();
        if (identity is null)
            return Unauthorized(new { error = "invalid_bridge_identity" });

        var register = await db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .SingleOrDefaultAsync(
                x => x.Id == identity.Value.CashRegisterId &&
                     x.BridgeInstallationId == identity.Value.InstallationId,
                cancellationToken);

        if (register is null ||
            !register.IsActive ||
            !register.Branch.IsActive ||
            register.PosTerminal is null ||
            !register.PosTerminal.IsActive)
        {
            return Unauthorized(new
            {
                error = "bridge_or_terminal_inactive",
                message = "The bridge installation or assigned POS terminal is inactive."
            });
        }

        return Ok(new
        {
            installationId = identity.Value.InstallationId,
            cashRegister = new
            {
                id = register.Id,
                name = register.Name,
                branchId = register.BranchId,
                branchName = register.Branch.Name
            },
            terminal = new
            {
                id = register.PosTerminal.Id,
                name = register.PosTerminal.Name,
                ipAddress = register.PosTerminal.IpAddress,
                port = register.PosTerminal.Port,
                connectionType = register.PosTerminal.ConnectionType,
                terminalCode = register.PosTerminal.TerminalCode
            }
        });
    }

    /// <summary>
    /// Returns the payment already claimed by this Bridge installation, if any.
    /// This endpoint is read-only and never claims or changes a payment.
    /// </summary>
    [HttpGet("payments/active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActivePayment(CancellationToken cancellationToken)
    {
        DisableCaching();

        var identity = GetBridgeIdentity();
        if (identity is null)
            return Unauthorized(new { error = "invalid_bridge_identity" });

        var payment = await db.PaymentTransactions
            .AsNoTracking()
            .Where(x =>
                x.CashRegisterId == identity.Value.CashRegisterId &&
                x.BridgeInstallationId == identity.Value.InstallationId &&
                x.Status == PaymentTransactionStatus.Processing)
            .OrderByDescending(x => x.ProcessingAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                paymentId = x.Id,
                merchantReference = x.MerchantReference,
                amount = x.Amount,
                currency = "IRR",
                purpose = x.Purpose.ToString()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
            return NoContent();

        if (payment.amount <= 0 ||
            decimal.Truncate(payment.amount) != payment.amount ||
            payment.amount > long.MaxValue)
        {
            return Conflict(new { error = "invalid_active_payment_amount" });
        }

        return Ok(new
        {
            payment.paymentId,
            payment.merchantReference,
            amount = decimal.ToInt64(payment.amount),
            payment.currency,
            payment.purpose
        });
    }

    /// <summary>
    /// Atomically claims one queued payment for this authenticated cash register.
    /// A claimed payment is never automatically re-queued because the POS may already
    /// have received the sale command.
    /// </summary>
    [HttpGet("payments/next")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetNextPayment(CancellationToken cancellationToken)
    {
        DisableCaching();

        var identity = GetBridgeIdentity();
        if (identity is null)
            return Unauthorized(new { error = "invalid_bridge_identity" });

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var currentTerminalId = await db.CashRegisters
            .Where(x => x.Id == identity.Value.CashRegisterId)
            .Select(x => x.PosTerminalId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!currentTerminalId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return Unauthorized(new { error = "cash_register_has_no_terminal" });
        }

        // UPDLOCK + READPAST prevents two Bridge instances from claiming the same row.
        // The terminal ID check prevents a queued request from being sent to a
        // different terminal after an administrator reassigns the cash register.
        var payment = await db.PaymentTransactions
            .FromSqlInterpolated($@"
                SELECT TOP (1) *
                FROM [PaymentTransactions] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK)
                WHERE [CashRegisterId] = {identity.Value.CashRegisterId}
                  AND [PosTerminalId] = {currentTerminalId.Value}
                  AND [Status] = {(int)PaymentTransactionStatus.Created}
                ORDER BY [CreatedAtUtc], [Id]")
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }

        if (payment.Amount <= 0 || decimal.Truncate(payment.Amount) != payment.Amount)
        {
            payment.Status = PaymentTransactionStatus.ReconciliationRequired;
            payment.CompletedAtUtc = DateTime.UtcNow;
            payment.ResponseMessage = "مبلغ تراکنش عدد صحیح ریال نیست؛ پرداخت به کارت‌خوان ارسال نشد.";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Conflict(new
            {
                error = "invalid_payment_amount",
                paymentId = payment.Id
            });
        }

        payment.Status = PaymentTransactionStatus.Processing;
        payment.ProcessingAtUtc = DateTime.UtcNow;
        payment.BridgeInstallationId = identity.Value.InstallationId;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            paymentId = payment.Id,
            merchantReference = payment.MerchantReference,
            amount = decimal.ToInt64(payment.Amount),
            currency = "IRR",
            purpose = payment.Purpose.ToString(),
            orderId = payment.OrderId,
            createdAtUtc = payment.CreatedAtUtc
        });
    }

    /// <summary>
    /// Reports the terminal result. Only the Bridge installation that claimed
    /// the payment may report it. Repeated reports are idempotent.
    /// </summary>
    [HttpPost("payments/{id:long}/result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportPaymentResult(
        long id,
        [FromBody] BridgePaymentResultRequest request,
        CancellationToken cancellationToken)
    {
        DisableCaching();

        var identity = GetBridgeIdentity();
        if (identity is null)
            return Unauthorized(new { error = "invalid_bridge_identity" });

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payment = await db.PaymentTransactions
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (payment is null)
            return NotFound(new { error = "payment_not_found" });

        if (!string.Equals(
                payment.BridgeInstallationId,
                identity.Value.InstallationId,
                StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (payment.Status != PaymentTransactionStatus.Processing &&
            !(payment.Status == PaymentTransactionStatus.Unknown && request.ResultReceived))
        {
            // The Bridge may retry after a lost HTTP response. Do not apply the
            // same successful payment to the order a second time.
            if (payment.Status is PaymentTransactionStatus.Succeeded
                or PaymentTransactionStatus.Failed
                or PaymentTransactionStatus.Cancelled
                or PaymentTransactionStatus.Unknown
                or PaymentTransactionStatus.ReconciliationRequired)
            {
                await transaction.CommitAsync(cancellationToken);
                return Ok(new
                {
                    paymentId = payment.Id,
                    status = payment.Status.ToString(),
                    alreadyProcessed = true
                });
            }

            return Conflict(new
            {
                error = "payment_not_processing",
                status = payment.Status.ToString()
            });
        }

        payment.ResponseCode = Limit(request.ResponseCode, 50);
        payment.Rrn = Limit(request.Rrn, 100);
        payment.TraceNumber = Limit(request.TransactionNo, 100);

        var responseMessage = string.Join(" | ", new[]
        {
            request.PcPosStatusCode,
            request.PcPosStatus,
            request.OptionalField
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        payment.ResponseMessage = Limit(responseMessage, 1000);
        payment.CompletedAtUtc = DateTime.UtcNow;

        var responseCode = request.ResponseCode?.Trim();
        var successTextReceived = request.OptionalField?.Contains(
            "تراکنش موفق",
            StringComparison.Ordinal) == true;

        var isConfirmedSuccess =
            request.ResultReceived &&
            string.Equals(responseCode, "00", StringComparison.Ordinal) &&
            successTextReceived;

        // PcPosStatusCode=3 is the terminal's explicit "cancelled by POS user"
        // result. It is a definitive cancellation, not an ambiguous outcome.
        var isDefinitiveCancellation =
            request.ResultReceived &&
            string.Equals(
                request.PcPosStatusCode?.Trim(),
                "3",
                StringComparison.Ordinal);

        var isDefinitiveFailure =
            request.ResultReceived &&
            !string.IsNullOrWhiteSpace(responseCode) &&
            !string.Equals(responseCode, "00", StringComparison.Ordinal);

        if (isConfirmedSuccess)
        {
            var availableAmount = payment.Purpose == PaymentPurpose.Deposit
                ? Math.Max(0, payment.Order.RequestedDepositAmount - payment.Order.DepositAmount)
                : payment.Order.RemainingAmount;

            if (payment.Amount > availableAmount)
            {
                payment.Status = PaymentTransactionStatus.ReconciliationRequired;
                payment.ResponseMessage = Limit(
                    (payment.ResponseMessage ?? "") +
                    " | کارت‌خوان موفقیت را اعلام کرده اما مبلغ درخواستی از مانده مجاز این نوع پرداخت بیشتر است؛ بررسی دستی الزامی است.",
                    1000);
            }
            else
            {
                payment.Status = PaymentTransactionStatus.Succeeded;
                payment.Order.PaidAmount += payment.Amount;
                if (payment.Purpose == PaymentPurpose.Deposit)
                {
                    payment.Order.DepositAmount += payment.Amount;

                    // Only a fully paid requested deposit triggers automatic confirmation.
                    if (payment.Order.Status == OrderStatus.PendingReview &&
                        payment.Order.RequestedDepositAmount > 0 &&
                        payment.Order.DepositAmount >= payment.Order.RequestedDepositAmount)
                    {
                        var previousOrderStatus = payment.Order.Status;
                        var now = DateTime.UtcNow;
                        payment.Order.Status = OrderStatus.Confirmed;
                        payment.Order.UpdatedByUserId = null;
                        payment.Order.StatusHistory.Add(new OrderStatusHistory
                        {
                            PreviousStatus = previousOrderStatus,
                            NewStatus = OrderStatus.Confirmed,
                            ChangedAtUtc = now,
                            ChangedByUserId = null,
                            Note = "تأیید خودکار سیستمی: بیعانه درخواستی به‌طور کامل پرداخت و پاسخ موفق قطعی کارت‌خوان دریافت شد."
                        });

                        if (payment.Order.Kind == OrderKind.BirthdayCake &&
                            payment.Order.ProductionBranchId is long branchId &&
                            !await db.ProductionTasks.AnyAsync(x => x.OrderId == payment.OrderId, cancellationToken))
                        {
                            var workshopId = await db.Workshops
                                .Where(x => x.BranchId == branchId && x.IsActive && x.Name == "کیک‌سازی")
                                .Select(x => (long?)x.Id)
                                .SingleOrDefaultAsync(cancellationToken);

                            if (workshopId.HasValue)
                            {
                                var cakeTypeText = payment.Order.CakeType switch
                                {
                                    CakeType.Cream => "خامه‌ای",
                                    CakeType.SemiFondant => "نیمه فوندانت",
                                    CakeType.FullFondant => "تمام فوندانت",
                                    _ => "نامشخص"
                                };
                                var notes = string.Join(" | ", new[]
                                {
                                    $"نوع سفارش: {payment.Order.OrderType}",
                                    $"نوع کیک: {cakeTypeText}",
                                    $"وزن درخواستی: {payment.Order.RequestedWeightKg:0.##} کیلوگرم",
                                    string.IsNullOrWhiteSpace(payment.Order.BackgroundColor) ? null : $"رنگ زمینه: {payment.Order.BackgroundColor}",
                                    string.IsNullOrWhiteSpace(payment.Order.MoldType) ? null : $"قالب: {payment.Order.MoldType}",
                                    string.IsNullOrWhiteSpace(payment.Order.PrintType) ? null : $"نوع چاپ: {payment.Order.PrintType}",
                                    payment.Order.HasBananaFilling ? "فیلینگ موز" : null,
                                    payment.Order.HasWalnutFilling ? "فیلینگ گردو" : null,
                                    string.IsNullOrWhiteSpace(payment.Order.CakeText) ? null : $"متن روی کیک: {payment.Order.CakeText}",
                                    string.IsNullOrWhiteSpace(payment.Order.DesignNotes) ? null : $"توضیحات طرح: {payment.Order.DesignNotes}"
                                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                                var task = new ProductionTask
                                {
                                    OrderId = payment.OrderId,
                                    BranchId = branchId,
                                    WorkshopId = workshopId.Value,
                                    Status = ProductionTaskStatus.AwaitingWorkshopReview,
                                    CreatedAtUtc = now,
                                    CreatedByUserId = null
                                };
                                task.Items.Add(new ProductionTaskItem
                                {
                                    ProductName = "کیک تولد",
                                    Unit = ProductUnit.Piece,
                                    Quantity = 1,
                                    Notes = notes
                                });
                                task.History.Add(new ProductionTaskHistory
                                {
                                    PreviousStatus = null,
                                    NewStatus = ProductionTaskStatus.AwaitingWorkshopReview,
                                    ChangedAtUtc = now,
                                    ChangedByUserId = null,
                                    Note = "وظیفه تولید پس از تکمیل بیعانه به‌صورت خودکار ایجاد و برای بررسی به کارگاه ارجاع شد."
                                });
                                db.ProductionTasks.Add(task);
                            }
                        }
                    }
                }
            }
        }
        else if (isDefinitiveCancellation)
        {
            payment.Status = PaymentTransactionStatus.Cancelled;
        }
        else if (isDefinitiveFailure)
        {
            payment.Status = PaymentTransactionStatus.Failed;
        }
        else
        {
            // Missing/ambiguous response is not treated as a decline and must
            // block another charge until a human reconciles the terminal.
            payment.Status = PaymentTransactionStatus.Unknown;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            paymentId = payment.Id,
            status = payment.Status.ToString(),
            appliedToOrder = payment.Status == PaymentTransactionStatus.Succeeded,
            message = payment.Status switch
            {
                PaymentTransactionStatus.Succeeded => payment.Purpose == PaymentPurpose.Deposit ? "بیعانه با موفقیت تأیید و به سفارش اضافه شد." : "تسویه با موفقیت تأیید شد.",
                PaymentTransactionStatus.Failed => "پایانه پرداخت را ناموفق اعلام کرد.",
                PaymentTransactionStatus.Cancelled => "تراکنش توسط کاربر کارت‌خوان لغو شد؛ امکان ثبت درخواست پرداخت جدید وجود دارد.",
                PaymentTransactionStatus.Unknown => "نتیجه پرداخت نامشخص است؛ پیش از تلاش مجدد باید بررسی شود.",
                PaymentTransactionStatus.ReconciliationRequired => "پرداخت نیازمند بررسی و تطبیق دستی است.",
                _ => "نتیجه ثبت شد."
            }
        });
    }

    private (long CashRegisterId, string InstallationId)? GetBridgeIdentity()
    {
        var cashRegisterIdValue = User.FindFirstValue(
            BridgeAuthenticationDefaults.CashRegisterIdClaim);
        var installationId = User.FindFirstValue(
            BridgeAuthenticationDefaults.InstallationIdClaim);

        if (!long.TryParse(cashRegisterIdValue, out var cashRegisterId) ||
            string.IsNullOrWhiteSpace(installationId))
            return null;

        return (cashRegisterId, installationId);
    }

    private void DisableCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class BridgePaymentResultRequest
{
    public bool ResultReceived { get; set; }

    [StringLength(50)]
    public string? ResponseCode { get; set; }

    [StringLength(100)]
    public string? TransactionNo { get; set; }

    [StringLength(100)]
    public string? Rrn { get; set; }

    [StringLength(100)]
    public string? PcPosStatusCode { get; set; }

    [StringLength(500)]
    public string? PcPosStatus { get; set; }

    [StringLength(500)]
    public string? OptionalField { get; set; }
}