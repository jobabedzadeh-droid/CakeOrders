using System.Globalization;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CakeOrders.Web.Services;

/// <summary>
/// Posts one FIN receipt for every successfully recorded deposit payment.
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
        var masterData = scope.ServiceProvider.GetRequiredService<IRahkaranMasterDataClient>();
        var cashClient = scope.ServiceProvider.GetRequiredService<IRahkaranReceiptClient>();
        var posClient = scope.ServiceProvider.GetRequiredService<IRahkaranDepositReceiptClient>();

        var entry = await db.RahkaranReceiptOutbox
            .Include(x => x.Order)
            .SingleOrDefaultAsync(x => x.Id == outboxId, ct);
        if (entry is null || entry.Status != RahkaranReceiptSyncStatus.Processing)
            return;

        try
        {
            var order = entry.Order;
            if (string.IsNullOrWhiteSpace(order.RahkaranCustomerId) ||
                !long.TryParse(order.RahkaranCustomerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var customerId) ||
                customerId <= 0)
                throw new InvalidOperationException("شناسه مشتری راهکاران روی سفارش ثبت نشده است.");

            var customers = await masterData.GetCustomersAsync(ct);
            var customerMatches = customers.Where(x => x.Id == customerId).ToList();
            if (customerMatches.Count != 1 || string.IsNullOrWhiteSpace(customerMatches[0].DLCode))
                throw new InvalidOperationException("کد تفصیلی مشتری از راهکاران به‌صورت یکتا دریافت نشد؛ ابتدا اطلاعات مشتری را بررسی کنید.");

            var settings = await db.RahkaranIntegrationSettings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, ct)
                ?? throw new InvalidOperationException("تنظیمات مالی راهکاران ثبت نشده است.");

            if (settings.BranchId is not > 0 || settings.CashId is not > 0)
                throw new InvalidOperationException("شناسه شعبه و صندوق راهکاران باید در تنظیمات ثبت شود.");

            var customerCode = customerMatches[0].DLCode.Trim();
            var localDate = new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAtUtc, DateTimeKind.Utc))
                .ToOffset(TimeSpan.FromHours(3.5));
            var documentNumber = $"CO-{order.OrderNumber}-{(entry.OrderCashPaymentId ?? entry.PaymentTransactionId ?? entry.Id).ToString(CultureInfo.InvariantCulture)}";
            var description = $"بیعانه سفارش {order.OrderNumber} - {(entry.Source == RahkaranReceiptSource.Cash ? "نقدی" : "کارت‌خوان")}";

            RahkaranReceiptResult result;
            if (entry.Source == RahkaranReceiptSource.Cash)
            {
                if (settings.CashAccountingOperationId is not > 0 || settings.CashFlowFactorId is not > 0)
                    throw new InvalidOperationException("عملیات حسابداری و عامل گردش نقدینگی دریافت نقدی در تنظیمات راهکاران تکمیل نشده است.");

                result = await cashClient.RegisterCashReceiptAsync(_options.ServiceBaseUrl,
                    new RahkaranCashReceiptRequest
                    {
                        BranchID = settings.BranchId.Value,
                        CashID = settings.CashId.Value,
                        CounterPartDLCode = customerCode,
                        Date = localDate,
                        Description = description,
                        Description_En = $"Deposit for order {order.OrderNumber} - Cash",
                        IsApproved = true,
                        Number = documentNumber,
                        ReceiptCashMoneys =
                        [
                            new RahkaranCashReceiptItem
                            {
                                AccountingOperationID = settings.CashAccountingOperationId.Value,
                                Amount = entry.Amount,
                                CashFlowFactorID = settings.CashFlowFactorId.Value,
                                CounterPartDLCode = customerCode,
                                Description = description,
                                Description_En = $"Deposit for order {order.OrderNumber}"
                            }
                        ]
                    }, ct);
            }
            else
            {
                if (settings.PosBankAccountId is not > 0 || settings.PosAccountingOperationId is not > 0)
                    throw new InvalidOperationException("حساب بانکی مقصد و عملیات حسابداری کارت‌خوان در تنظیمات راهکاران تکمیل نشده است.");

                result = await posClient.RegisterDepositReceiptAsync(_options.ServiceBaseUrl,
                    new RahkaranDepositReceiptRequest
                    {
                        BranchID = settings.BranchId.Value,
                        CashID = settings.CashId.Value,
                        CounterPartDLCode = customerCode,
                        Date = localDate,
                        Description = description,
                        Description_En = $"Deposit for order {order.OrderNumber} - POS",
                        IsApproved = true,
                        Number = documentNumber,
                        ReceiptDeposits =
                        [
                            new RahkaranDepositReceiptItem
                            {
                                AccountingOperationID = settings.PosAccountingOperationId.Value,
                                Amount = entry.Amount,
                                BankAccountID = settings.PosBankAccountId.Value,
                                CashFlowFactorID = settings.PosCashFlowFactorId,
                                CounterPartDLCode = customerCode,
                                Date = localDate,
                                Description = description,
                                Description_En = $"Deposit for order {order.OrderNumber}",
                                Number = documentNumber
                            }
                        ]
                    }, ct);
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
