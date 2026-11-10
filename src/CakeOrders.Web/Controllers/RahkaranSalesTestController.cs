using System.Globalization;
using System.Text.Json;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using CakeOrders.Web.ViewModels.Integrations.Rahkaran;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RahkaranSalesTestController(
    CakeOrdersDbContext db,
    IRahkaranSalesRequestClient client,
    ILogger<RahkaranSalesTestController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        RahkaranSalesTestViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.OrderId.GetValueOrDefault() <= 0)
            ModelState.AddModelError(nameof(model.OrderId), "یک سفارش انتخاب کنید.");

        if (model.QuantityKg.GetValueOrDefault() <= 0)
            ModelState.AddModelError(nameof(model.QuantityKg), "وزن آزمایشی باید بیشتر از صفر باشد.");

        if (model.PricePerKg.GetValueOrDefault() <= 0)
            ModelState.AddModelError(nameof(model.PricePerKg), "قیمت هر کیلو باید بیشتر از صفر باشد.");

        if (!ModelState.IsValid)
        {
            model.Orders = await LoadOrderOptionsAsync(cancellationToken);
            return View("Index", model);
        }

        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.Id == model.OrderId, cancellationToken);

        if (order is null)
            return await ShowErrorAsync(model, "سفارش انتخاب‌شده پیدا نشد.", cancellationToken);

        if (order.Kind != OrderKind.BirthdayCake)
            return await ShowErrorAsync(model, "این آزمایش فقط برای سفارش کیک تولد طراحی شده است.", cancellationToken);

        if (order.Customer is null ||
            string.IsNullOrWhiteSpace(order.Customer.RahkaranCustomerId) ||
            !long.TryParse(order.Customer.RahkaranCustomerId, out var customerId) ||
            customerId <= 0)
            return await ShowErrorAsync(model, "مشتری سفارش شناسه معتبر راهکاران ندارد.", cancellationToken);

        // Require the exact order number as a deliberate confirmation because this action
        // creates a real pending sales request in Rahkaran.
        if (!string.Equals(model.Confirmation?.Trim(), order.OrderNumber, StringComparison.Ordinal))
            return await ShowErrorAsync(model, "برای تأیید ارسال واقعی، شماره سفارش را دقیقاً در کادر تأیید وارد کنید.", cancellationToken);

        var settings = await db.RahkaranIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (settings is null)
            return await ShowErrorAsync(model, "تنظیمات راهکاران پیدا نشد.", cancellationToken);

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
        if (productId is not > 0) missing.Add("محصول");
        if (unitId is not > 0) missing.Add("واحد");
        if (missing.Count > 0)
            return await ShowErrorAsync(model, "نگاشت‌های لازم تکمیل نیست: " + string.Join("، ", missing), cancellationToken);

        var addressId = ResolveDefaultAddressId(order.Customer.AddressesJson);
        if (string.IsNullOrWhiteSpace(addressId))
            return await ShowErrorAsync(model,
                "شناسه نشانی پیش‌فرض مشتری در اطلاعات ذخیره‌شده پیدا نشد. ابتدا اطلاعات کامل مشتری را از راهکاران همگام‌سازی کنید.",
                cancellationToken);

        var quantity = model.QuantityKg!.Value;
        var pricePerKg = model.PricePerKg!.Value;
        var total = decimal.Round(quantity * pricePerKg, 2, MidpointRounding.AwayFromZero);
        var localDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3.5));

        var request = new RahkaranSaleRequest
        {
            CustomerId = customerId.ToString(CultureInfo.InvariantCulture),
            CurrencyId = settings.SalesCurrencyId!.Value.ToString(CultureInfo.InvariantCulture),
            Date = $"/Date({localDate.ToUnixTimeMilliseconds()}+0330)/",
            InventoryId = settings.SalesInventoryId!.Value.ToString(CultureInfo.InvariantCulture),
            PlantId = settings.SalesPlantId!.Value.ToString(CultureInfo.InvariantCulture),
            SalesOfficeId = settings.SalesOfficeId!.Value.ToString(CultureInfo.InvariantCulture),
            SalesAreaId = settings.SalesAreaId!.Value.ToString(CultureInfo.InvariantCulture),
            SalesTypeId = settings.SalesTypeId!.Value.ToString(CultureInfo.InvariantCulture),
            PaymentAgreementId = settings.SalesPaymentAgreementId?.ToString(CultureInfo.InvariantCulture),
            Price = total,
            NetPrice = total,
            AddressId = addressId,
            Description = $"TEST ONLY - CakeOrders Rahkaran integration test - source order {order.OrderNumber}",
            Items =
            [
                new RahkaranSaleRequestItem
                {
                    ProductId = productId!.Value.ToString(CultureInfo.InvariantCulture),
                    UnitId = unitId!.Value.ToString(CultureInfo.InvariantCulture),
                    Quantity = quantity.ToString(CultureInfo.InvariantCulture),
                    RequestedQuantity = quantity.ToString(CultureInfo.InvariantCulture),
                    SalesAreaId = settings.SalesAreaId!.Value.ToString(CultureInfo.InvariantCulture),
                    Fee = pricePerKg,
                    Price = total,
                    NetPrice = total,
                    Description = $"TEST ONLY - کیک {order.CakeType} - سفارش مرجع {order.OrderNumber}"
                }
            ]
        };

        logger.LogWarning(
            "Manual Rahkaran sales-request test initiated by {User}. SourceOrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={RahkaranCustomerId}, QuantityKg={QuantityKg}, PricePerKg={PricePerKg}, Total={Total}.",
            User.Identity?.Name, order.Id, order.OrderNumber, customerId, quantity, pricePerKg, total);

        try
        {
            // This is an actual POST to Rahkaran. It intentionally bypasses local order-status,
            // deposit and Outbox gates, and does not modify CakeOrders orders or Outbox records.
            var result = await client.PlacePendingAsync(request, cancellationToken);

            model.Succeeded = result.Succeeded;
            model.HttpStatusCode = result.HttpStatusCode;
            model.RemoteSalesRequestId = result.SalesRequestId;
            model.ResponseBody = result.Error;
            model.ResultMessage = result.Succeeded
                ? "درخواست آزمایشی در راهکاران ایجاد شد. شناسه دریافتی را نگهداری کنید."
                : "ارسال موفق تأیید نشد. نتیجه ممکن است مبهم باشد؛ تا بررسی وضعیت در راهکاران، درخواست را دوباره ارسال نکنید.";

            logger.LogWarning(
                "Manual Rahkaran sales-request test finished. SourceOrderId={OrderId}, Succeeded={Succeeded}, HttpStatusCode={HttpStatusCode}, RemoteSalesRequestId={RemoteSalesRequestId}.",
                order.Id, result.Succeeded, result.HttpStatusCode, result.SalesRequestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Manual Rahkaran sales-request test failed. SourceOrderId={OrderId}.",
                order.Id);
            model.Succeeded = false;
            model.ResultMessage = "خطا هنگام ارتباط با راهکاران رخ داد. اگر درخواست به سرویس رسیده باشد، نتیجه نامشخص است؛ پیش از تلاش مجدد وضعیت را در راهکاران بررسی کنید.";
            model.ResponseBody = ex.Message;
        }

        model.Orders = await LoadOrderOptionsAsync(cancellationToken);
        return View("Index", model);
    }

    private async Task<IActionResult> ShowErrorAsync(
        RahkaranSalesTestViewModel model,
        string message,
        CancellationToken cancellationToken)
    {
        model.Succeeded = false;
        model.ResultMessage = message;
        model.Orders = await LoadOrderOptionsAsync(cancellationToken);
        return View("Index", model);
    }

    private async Task<RahkaranSalesTestViewModel> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        return new RahkaranSalesTestViewModel
        {
            Orders = await LoadOrderOptionsAsync(cancellationToken)
        };
    }

    private async Task<IReadOnlyList<RahkaranSalesTestOrderOption>> LoadOrderOptionsAsync(
        CancellationToken cancellationToken)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.Kind == OrderKind.BirthdayCake &&
                        x.Customer != null &&
                        x.Customer.RahkaranCustomerId != null &&
                        x.Customer.RahkaranCustomerId != "")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return orders.Select(x => new RahkaranSalesTestOrderOption(
            x.Id,
            x.OrderNumber,
            x.Customer?.FullName ?? x.CustomerName,
            x.Customer?.RahkaranCustomerId,
            x.CakeType.ToString())).ToList();
    }

    private static string? ResolveDefaultAddressId(string? addressesJson)
    {
        if (string.IsNullOrWhiteSpace(addressesJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(addressesJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(root, "addresses", out var addresses) ||
                    TryGetProperty(root, "Addresses", out addresses))
                    root = addresses;
            }

            if (root.ValueKind != JsonValueKind.Array)
                return null;

            string? firstId = null;
            foreach (var address in root.EnumerateArray())
            {
                if (address.ValueKind != JsonValueKind.Object)
                    continue;

                var id = GetString(address, "addressId") ?? GetString(address, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                firstId ??= id;
                if (TryGetProperty(address, "isDefault", out var isDefault) &&
                    isDefault.ValueKind == JsonValueKind.True)
                    return id;
            }

            return firstId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
