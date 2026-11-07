using System.Globalization;
using System.Text;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CakeOrders.Web.Services;

public sealed class RahkaranSalesRequestOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RahkaranOptions> options,
    ILogger<RahkaranSalesRequestOutboxWorker> logger) : BackgroundService
{
    private readonly RahkaranOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled && _options.SalesRequestDispatchEnabled)
                    await ProcessOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Rahkaran sales-request outbox cycle failed."); }

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

            // A worker crash after claiming an item is ambiguous: never resend it automatically.
            var stale = await db.RahkaranSalesRequestOutbox
                .Where(x => x.Status == RahkaranSalesRequestSyncStatus.Processing &&
                            x.ProcessingStartedAtUtc < DateTime.UtcNow.AddMinutes(-10))
                .ToListAsync(ct);
            foreach (var item in stale)
            {
                item.Status = RahkaranSalesRequestSyncStatus.NeedsReview;
                item.CompletedAtUtc = DateTime.UtcNow;
                item.LastError = "پردازش قبلی بیش از ۱۰ دقیقه بدون نتیجه مانده است؛ برای جلوگیری از ثبت تکراری، ارسال خودکار متوقف شد و تطبیق دستی لازم است.";
            }

            // A manually confirmed order may still have an outstanding deposit.
            // Dispatch only when no deposit was requested or the requested deposit is fully received.
            var candidate = await db.RahkaranSalesRequestOutbox
                .FromSqlRaw(@"SELECT TOP (1) o.*
                              FROM [RahkaranSalesRequestOutbox] o WITH (UPDLOCK, READPAST, ROWLOCK)
                              INNER JOIN [Orders] ord ON ord.[Id] = o.[OrderId]
                              WHERE o.[Status] = 1
                                AND (o.[NextAttemptAtUtc] IS NULL OR o.[NextAttemptAtUtc] <= SYSUTCDATETIME())
                                AND (
                                    ord.[RequestedDepositAmount] <= 0
                                    OR (
                                        ord.[DepositAmount] >= ord.[RequestedDepositAmount]
                                        AND EXISTS (
                                            SELECT 1
                                            FROM [RahkaranReceiptOutbox] r
                                            LEFT JOIN [OrderCashPayments] cp
                                                ON cp.[Id] = r.[OrderCashPaymentId]
                                            LEFT JOIN [PaymentTransactions] pt
                                                ON pt.[Id] = r.[PaymentTransactionId]
                                            WHERE r.[OrderId] = ord.[Id]
                                              AND (
                                                  (r.[OrderCashPaymentId] IS NOT NULL AND cp.[Purpose] = 1)
                                                  OR
                                                  (r.[PaymentTransactionId] IS NOT NULL AND pt.[Purpose] = 1)
                                              )
                                        )
                                        AND NOT EXISTS (
                                            SELECT 1
                                            FROM [RahkaranReceiptOutbox] r
                                            LEFT JOIN [OrderCashPayments] cp
                                                ON cp.[Id] = r.[OrderCashPaymentId]
                                            LEFT JOIN [PaymentTransactions] pt
                                                ON pt.[Id] = r.[PaymentTransactionId]
                                            WHERE r.[OrderId] = ord.[Id]
                                              AND (
                                                  (r.[OrderCashPaymentId] IS NOT NULL AND cp.[Purpose] = 1)
                                                  OR
                                                  (r.[PaymentTransactionId] IS NOT NULL AND pt.[Purpose] = 1)
                                              )
                                              AND r.[Status] <> 3
                                        )
                                    )
                                )
                              ORDER BY o.[CreatedAtUtc], o.[Id]")
                .FirstOrDefaultAsync(ct);

            if (candidate is not null)
            {
                candidate.Status = RahkaranSalesRequestSyncStatus.Processing;
                candidate.AttemptCount++;
                candidate.ProcessingStartedAtUtc = DateTime.UtcNow;
                candidate.LastAttemptAtUtc = DateTime.UtcNow;
                candidate.LastError = null;
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
        var client = scope.ServiceProvider.GetRequiredService<IRahkaranSalesRequestClient>();

        var entry = await db.RahkaranSalesRequestOutbox
            .Include(x => x.Order)
                .ThenInclude(x => x.Customer)
            .SingleOrDefaultAsync(x => x.Id == outboxId, ct);
        if (entry is null || entry.Status != RahkaranSalesRequestSyncStatus.Processing) return;

        try
        {
            var order = entry.Order;
            if (order.Status != OrderStatus.Confirmed)
                throw new InvalidOperationException("درخواست فروش فقط برای سفارش تأییدشده مجاز است.");

            var settings = await db.RahkaranIntegrationSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct)
                ?? throw new InvalidOperationException("تنظیمات اتصال راهکاران ثبت نشده است.");

            var localCustomer = order.Customer;
            if (localCustomer is not null &&
                !string.IsNullOrWhiteSpace(localCustomer.RahkaranCustomerId))
            {
                if (!string.IsNullOrWhiteSpace(order.RahkaranCustomerId) &&
                    !string.Equals(order.RahkaranCustomerId, localCustomer.RahkaranCustomerId, StringComparison.Ordinal))
                    throw new InvalidOperationException("شناسه مشتری راهکاران در سفارش با پرونده مشتری داخلی مغایرت دارد.");

                order.RahkaranCustomerId = localCustomer.RahkaranCustomerId;
            }

            if (string.IsNullOrWhiteSpace(order.RahkaranCustomerId))
            {
                var mobile = NormalizeMobile(order.CustomerPhone);
                if (mobile.Length < 10)
                    throw new InvalidOperationException("شماره موبایل مشتری برای تطبیق با راهکاران معتبر نیست.");

                var customers = await masterData.GetCustomersAsync(ct);
                var matches = customers.Where(x => NormalizeMobile(x.Mobile) == mobile).ToList();
                if (matches.Count == 1)
                {
                    order.RahkaranCustomerId = matches[0].Id.ToString(CultureInfo.InvariantCulture);
                    if (localCustomer is not null)
                    {
                        localCustomer.RahkaranCustomerId = order.RahkaranCustomerId;
                        localCustomer.RahkaranPartyId = matches[0].PartyId ?? matches[0].PartyRef;
                        localCustomer.RahkaranCustomerNumber = matches[0].Number;
                        localCustomer.RahkaranSecondCode = matches[0].SecondCode;
                    }
                    await db.SaveChangesAsync(ct);
                }
                else if (matches.Count > 1)
                    throw new InvalidOperationException("چند مشتری با این شماره موبایل در راهکاران یافت شد؛ انتخاب مشتری باید دستی انجام شود.");
                else
                    throw new InvalidOperationException("مشتری در راهکاران یافت نشد. ایجاد خودکار به‌دلیل نداشتن آدرس و شناسه منطقه جغرافیایی معتبر در سفارش انجام نشد؛ ابتدا مشتری را در راهکاران ایجاد و شناسه آن را به سفارش متصل کنید.");
            }

            if (!long.TryParse(order.RahkaranCustomerId, out var customerId) || customerId <= 0)
                throw new InvalidOperationException("شناسه مشتری راهکاران روی سفارش معتبر نیست.");

            if (order.Kind != OrderKind.BirthdayCake)
                throw new InvalidOperationException("نگاشت محصول و واحد اقلام شیرینی هنوز در تنظیمات تعریف نشده است؛ ارسال خودکار این نوع سفارش متوقف شد.");

            var (productId, unitId) = order.CakeType switch
            {
                CakeType.Cream => (settings.CreamCakeProductId, settings.CreamCakeUnitId),
                CakeType.SemiFondant => (settings.SemiFondantCakeProductId, settings.SemiFondantCakeUnitId),
                CakeType.FullFondant => (settings.FullFondantCakeProductId, settings.FullFondantCakeUnitId),
                _ => (null, null)
            };

            var missing = new List<string>();
            if (settings.SalesOfficeId is not > 0) missing.Add("مرکز فروش");
            if (settings.SalesAreaId is not > 0) missing.Add("حوزه فروش");
            if (settings.SalesTypeId is not > 0) missing.Add("نحوه فروش");
            if (settings.SalesInventoryId is not > 0) missing.Add("انبار");
            if (settings.SalesPlantId is not > 0) missing.Add("مرکز نگهداری");
            if (settings.SalesCurrencyId is not > 0) missing.Add("ارز");
            if (productId is not > 0) missing.Add("محصول متناظر نوع کیک");
            if (unitId is not > 0) missing.Add("واحد کیلوگرم");
            if (order.RequestedWeightKg <= 0 || order.PricePerKg < 0 || order.FinalAmount < 0)
                missing.Add("مقادیر معتبر وزن/قیمت سفارش");
            if (missing.Count > 0)
                throw new InvalidOperationException("نگاشت یا داده‌های لازم تکمیل نیست: " + string.Join("، ", missing));

            var localDate = new DateTimeOffset(order.CreatedAtUtc).ToOffset(TimeSpan.FromHours(3.5));
            var milliseconds = localDate.ToUnixTimeMilliseconds();
            var request = new RahkaranSaleRequest
            {
                CustomerId = customerId.ToString(CultureInfo.InvariantCulture),
                CurrencyId = settings.SalesCurrencyId!.Value.ToString(CultureInfo.InvariantCulture),
                Date = $"/Date({milliseconds}+0330)/",
                InventoryId = settings.SalesInventoryId!.Value.ToString(CultureInfo.InvariantCulture),
                PlantId = settings.SalesPlantId!.Value.ToString(CultureInfo.InvariantCulture),
                SalesOfficeId = settings.SalesOfficeId!.Value.ToString(CultureInfo.InvariantCulture),
                SalesAreaId = settings.SalesAreaId!.Value.ToString(CultureInfo.InvariantCulture),
                SalesTypeId = settings.SalesTypeId!.Value.ToString(CultureInfo.InvariantCulture),
                PaymentAgreementId = settings.SalesPaymentAgreementId?.ToString(CultureInfo.InvariantCulture),
                Price = order.FinalAmount,
                NetPrice = order.FinalAmount,
                Description = $"سفارش CakeOrders: {order.OrderNumber}",
                Items =
                [
                    new RahkaranSaleRequestItem
                    {
                        ProductId = productId!.Value.ToString(CultureInfo.InvariantCulture),
                        UnitId = unitId!.Value.ToString(CultureInfo.InvariantCulture),
                        Quantity = order.RequestedWeightKg.ToString(CultureInfo.InvariantCulture),
                        RequestedQuantity = order.RequestedWeightKg.ToString(CultureInfo.InvariantCulture),
                        SalesAreaId = settings.SalesAreaId!.Value.ToString(CultureInfo.InvariantCulture),
                        Fee = order.PricePerKg,
                        Price = order.FinalAmount,
                        NetPrice = order.FinalAmount,
                        Description = $"کیک {order.CakeType} - {order.OrderNumber}"
                    }
                ]
            };

            // Once this POST starts, every non-success/ambiguous outcome is NeedsReview.
            // There is intentionally no automatic retry because the remote server may have created the request.
            var result = await client.PlacePendingAsync(request, ct);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.SalesRequestId))
                throw new InvalidOperationException(result.Error ?? "پاسخ راهکاران مبهم است؛ ارسال مجدد خودکار ممنوع است.");

            entry.Status = RahkaranSalesRequestSyncStatus.Succeeded;
            entry.RemoteSalesRequestId = result.SalesRequestId;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = null;
            order.RahkaranSalesRequestId = result.SalesRequestId;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A host shutdown during HTTP may have an unknown remote outcome.
            entry.Status = RahkaranSalesRequestSyncStatus.NeedsReview;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = "پردازش هنگام توقف برنامه قطع شد؛ وضعیت سمت راهکاران باید پیش از هر ارسال مجدد بررسی شود.";
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rahkaran sales request for CakeOrders outbox {OutboxId} requires review.", outboxId);
            entry.Status = RahkaranSalesRequestSyncStatus.NeedsReview;
            entry.CompletedAtUtc = DateTime.UtcNow;
            entry.ProcessingStartedAtUtc = null;
            entry.LastError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await db.SaveChangesAsync(ct);
        }
    }

    private static string NormalizeMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var digit = ch switch
            {
                >= '۰' and <= '۹' => (char)('0' + ch - '۰'),
                >= '٠' and <= '٩' => (char)('0' + ch - '٠'),
                _ => ch
            };
            if (char.IsDigit(digit)) builder.Append(digit);
        }
        var result = builder.ToString();
        if (result.StartsWith("0098")) result = result[4..];
        else if (result.StartsWith("98") && result.Length == 12) result = result[2..];
        if (result.Length == 10 && result[0] == '9') result = "0" + result;
        return result;
    }
}
