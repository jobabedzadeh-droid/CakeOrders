using System.Globalization;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CakeOrders.Web.Services;

/// <summary>
/// Posts one FIN receipt for every successfully recorded payment.
/// Ambiguous outcomes are never retried automatically.
/// </summary>
public sealed class RahkaranReceiptOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RahkaranOptions> options,
    ILogger<RahkaranReceiptOutboxWorker> logger) : BackgroundService
{
    private readonly RahkaranOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                    await ProcessOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rahkaran receipt outbox cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    /// <summary>
    /// Claims and dispatches exactly the requested pending receipt.
    /// This administrative path is available while automatic dispatch is disabled.
    /// The row is claimed under an update lock to prevent races with the background worker.
    /// </summary>
    public async Task<RahkaranReceiptSyncStatus?> DispatchSpecificAsync(
        long outboxId,
        CancellationToken cancellationToken = default)
    {
        if (outboxId <= 0)
            throw new ArgumentOutOfRangeException(nameof(outboxId));

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CakeOrdersDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var candidate = await db.RahkaranReceiptOutbox
                .FromSqlInterpolated($@"SELECT * FROM [RahkaranReceiptOutbox] WITH (UPDLOCK, READPAST, ROWLOCK)
                                        WHERE [Id] = {outboxId} AND [Status] = {(int)RahkaranReceiptSyncStatus.Pending}")
                .SingleOrDefaultAsync(cancellationToken);

            if (candidate is null)
                return null;

            candidate.Status = RahkaranReceiptSyncStatus.Processing;
            candidate.ProcessingStartedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await DispatchAsync(outboxId, cancellationToken);

        await using var resultScope = scopeFactory.CreateAsyncScope();
        var resultDb = resultScope.ServiceProvider.GetRequiredService<CakeOrdersDbContext>();
        return await resultDb.RahkaranReceiptOutbox
            .AsNoTracking()
            .Where(x => x.Id == outboxId)
            .Select(x => (RahkaranReceiptSyncStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task ProcessOneAsync(CancellationToken ct)
    {
        long? outboxId = null;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CakeOrdersDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var stale = await db.RahkaranReceiptOutbox
                .Where(x => x.Status == RahkaranReceiptSyncStatus.Processing &&
                            x.ProcessingStartedAtUtc < DateTime.UtcNow.AddMinutes(-10))
                .ToListAsync(ct);
            foreach (var item in stale)
            {
                item.Status = RahkaranReceiptSyncStatus.NeedsReview;
                item.CompletedAtUtc = DateTime.UtcNow;
                item.LastError = "پردازش قبلی بیش از ۱۰ دقیقه بدون نتیجه مانده است؛ برای جلوگیری از ثبت رسید تکراری، بررسی دستی راهکاران الزامی است.";
            }

            var candidate = await db.RahkaranReceiptOutbox
                .FromSqlRaw(@"SELECT TOP (1) * FROM [RahkaranReceiptOutbox] WITH (UPDLOCK, READPAST, ROWLOCK)
                              WHERE [Status] = 1
                              ORDER BY [CreatedAtUtc], [Id]")
                .FirstOrDefaultAsync(ct);

            if (candidate is not null)
            {
                candidate.Status = RahkaranReceiptSyncStatus.Processing;
                candidate.ProcessingStartedAtUtc = DateTime.UtcNow;
                outboxId = candidate.Id;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        if (outboxId.HasValue)
            await DispatchAsync(outboxId.Value, ct);
    }

    private async Task DispatchAsync(long outboxId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CakeOrdersDbContext>();
        var cashClient = scope.ServiceProvider.GetRequiredService<IRahkaranReceiptClient>();
        var posClient = scope.ServiceProvider.GetRequiredService<IRahkaranDepositReceiptClient>();

        var entry = await db.RahkaranReceiptOutbox
            .Include(x => x.Order)
                .ThenInclude(x => x.Customer)
            .SingleOrDefaultAsync(x => x.Id == outboxId, ct);
        if (entry is null || entry.Status != RahkaranReceiptSyncStatus.Processing)
            return;

        try
        {
            var order = entry.Order;
            var customer = order.Customer
                ?? throw new InvalidOperationException("مشتری داخلی سفارش به یک رکورد مشتری متصل نیست؛ ابتدا اطلاعات مشتریان راهکاران را همگام‌سازی و سفارش را بررسی کنید.");

            var storedCustomerId = customer.RahkaranCustomerId;
            if (!string.IsNullOrWhiteSpace(order.RahkaranCustomerId) &&
                !string.IsNullOrWhiteSpace(storedCustomerId) &&
                !string.Equals(order.RahkaranCustomerId, storedCustomerId, StringComparison.Ordinal))
                throw new InvalidOperationException("شناسه مشتری راهکاران در سفارش با شناسه ذخیره‌شده در پرونده مشتری یکسان نیست.");

            var customerIdValue = storedCustomerId ?? order.RahkaranCustomerId;
            if (!long.TryParse(customerIdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var customerId) ||
                customerId <= 0)
                throw new InvalidOperationException("شناسه مشتری راهکاران در پرونده مشتری ثبت نشده است؛ ابتدا همگام‌سازی اطلاعات مشتریان را اجرا کنید.");

            if (string.IsNullOrWhiteSpace(customer.RahkaranDLCode))
                throw new InvalidOperationException("کد تفصیلی مشتری در پایگاه داده CakeOrders موجود نیست؛ همگام‌سازی جزئیات مشتری را اجرا و کد تفصیلی را بررسی کنید.");

            order.RahkaranCustomerId = customerId.ToString(CultureInfo.InvariantCulture);

            var settings = await db.RahkaranIntegrationSettings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, ct)
                ?? throw new InvalidOperationException("تنظیمات مالی راهکاران ثبت نشده است.");

            if (settings.BranchId is not > 0 || settings.CashId is not > 0)
                throw new InvalidOperationException("شناسه شعبه و صندوق راهکاران باید در تنظیمات ثبت شود.");

            var customerCode = customer.RahkaranDLCode.Trim();
            var localDate = new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAtUtc, DateTimeKind.Utc))
                .ToOffset(TimeSpan.FromHours(3.5));
            var documentNumber = $"CO-{order.OrderNumber}-{(entry.OrderCashPaymentId ?? entry.PaymentTransactionId ?? entry.Id).ToString(CultureInfo.InvariantCulture)}";
            var isSettlement = entry.Purpose == PaymentPurpose.Settlement;
            var paymentPurposeTitle = isSettlement ? "تسویه" : "بیعانه";
            var description = $"دریافت {paymentPurposeTitle} سفارش {order.OrderNumber} - {(entry.Source == RahkaranReceiptSource.Cash ? "نقدی" : "کارت‌خوان")}";
            var cashOperationId = isSettlement
                ? settings.SettlementCashAccountingOperationId
                : settings.CashAccountingOperationId;
            var cashFlowFactorId = isSettlement
                ? settings.SettlementCashFlowFactorId
                : settings.CashFlowFactorId;
            var posBankAccountId = isSettlement
                ? settings.SettlementPosBankAccountId
                : settings.PosBankAccountId;
            var posOperationId = isSettlement
                ? settings.SettlementPosAccountingOperationId
                : settings.PosAccountingOperationId;
            var posCashFlowFactorId = isSettlement
                ? settings.SettlementPosCashFlowFactorId
                : settings.PosCashFlowFactorId;

            RahkaranReceiptResult result;
            if (entry.Source == RahkaranReceiptSource.Cash)
            {
                if (cashOperationId is not > 0 || cashFlowFactorId is not > 0)
                    throw new InvalidOperationException($"عملیات حسابداری و عامل گردش نقدینگی دریافت نقدی {paymentPurposeTitle} در تنظیمات راهکاران تکمیل نشده است.");

                result = await cashClient.RegisterCashReceiptAsync(_options.ServiceBaseUrl,
                    new RahkaranCashReceiptRequest
                    {
                        BranchID = settings.BranchId.Value,
                        CashID = settings.CashId.Value,
                        CounterPartDLCode = customerCode,
                        Date = localDate,
                        Description = description,
                        Description_En = $"{(isSettlement ? "Settlement" : "Deposit")} for order {order.OrderNumber} - Cash",
                        IsApproved = true,
                        Number = documentNumber,
                        ReceiptCashMoneys =
                        [
                            new RahkaranCashReceiptItem
                            {
                                AccountingOperationID = cashOperationId.Value,
                                Amount = entry.Amount,
                                CashFlowFactorID = cashFlowFactorId.Value,
                                CounterPartDLCode = customerCode,
                                Description = description,
                                Description_En = $"{(isSettlement ? "Settlement" : "Deposit")} for order {order.OrderNumber}"
                            }
                        ]
                    }, ct);
            }
            else if (entry.Source == RahkaranReceiptSource.PointOfSale)
            {
                if (posBankAccountId is not > 0 ||
                    posOperationId is not > 0 ||
                    posCashFlowFactorId is not > 0)
                {
                    throw new InvalidOperationException(
                        $"حساب بانکی، عملیات حسابداری و عامل گردش نقدینگی کارت‌خوان {paymentPurposeTitle} باید در تنظیمات راهکاران تکمیل شوند.");
                }

                result = await posClient.RegisterDepositReceiptAsync(_options.ServiceBaseUrl,
                    new RahkaranDepositReceiptRequest
                    {
                        BranchID = settings.BranchId.Value,
                        CashID = settings.CashId.Value,
                        CounterPartDLCode = customerCode,
                        Date = localDate,
                        Description = description,
                        Description_En = $"{(isSettlement ? "Settlement" : "Deposit")} for order {order.OrderNumber} - POS",
                        IsApproved = true,
                        Number = documentNumber,
                        ReceiptDeposits =
                        [
                            new RahkaranDepositReceiptItem
                            {
                                AccountingOperationID = posOperationId.Value,
                                Amount = entry.Amount,
                                BankAccountID = posBankAccountId.Value,
                                CashFlowFactorID = posCashFlowFactorId.Value,
                                CounterPartDLCode = customerCode,
                                Date = localDate,
                                Description = description,
                                Description_En = $"Payment for order {order.OrderNumber}",
                                Number = documentNumber
                            }
                        ]
                    }, ct);
            }
            else
            {
                throw new InvalidOperationException("روش دریافت وجه در صف راهکاران معتبر نیست.");
            }

            if (!result.Succeeded || result.ReceiptId is not > 0)
                throw new InvalidOperationException(result.Error ?? "پاسخ راهکاران برای ثبت رسید معتبر نبود؛ ارسال مجدد خودکار انجام نمی‌شود.");

            entry.Status = RahkaranReceiptSyncStatus.Succeeded;
            entry.RemoteReceiptId = result.ReceiptId.Value.ToString(CultureInfo.InvariantCulture);
            entry.RemoteReceiptNumber = result.ReceiptNumber;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = null;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            entry.Status = RahkaranReceiptSyncStatus.NeedsReview;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = "پردازش هنگام توقف برنامه قطع شد؛ نتیجه سمت راهکاران نامشخص است و باید دستی بررسی شود.";
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rahkaran receipt outbox {OutboxId} requires review.", outboxId);
            entry.Status = RahkaranReceiptSyncStatus.NeedsReview;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await db.SaveChangesAsync(ct);
        }
    }

}
