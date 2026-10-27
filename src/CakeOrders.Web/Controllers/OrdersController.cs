using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

public sealed class OrdersController(CakeOrdersDbContext db, IWebHostEnvironment environment) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

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

    [HttpGet]
    public async Task<IActionResult> LookupReferrer(string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { found = false, fullName = "" });

        var referrer = await db.Referrers.AsNoTracking()
            .Where(x => x.IsActive && x.Code == code.Trim())
            .Select(x => new { x.FirstName, x.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        return referrer is null
            ? Json(new { found = false, fullName = "" })
            : Json(new { found = true, fullName = (referrer.FirstName + " " + referrer.LastName).Trim() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Create(OrderCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.CalculatedTotal < 0)
            ModelState.AddModelError(nameof(model.Discount), "تخفیف نمی‌تواند از جمع هزینه‌ها بیشتر باشد.");
        if (model.DepositAmount > Math.Max(0, model.CalculatedTotal))
            ModelState.AddModelError(nameof(model.DepositAmount), "بیعانه نمی‌تواند از مبلغ نهایی بیشتر باشد.");

        ValidateImage(model.SampleImage, nameof(model.SampleImage));
        ValidateImage(model.PrintImage, nameof(model.PrintImage));

        if (!ModelState.IsValid)
            return View(model);

        string? sampleImagePath = null;
        string? printImagePath = null;
        try
        {
            sampleImagePath = await SaveImageAsync(model.SampleImage, cancellationToken);
            printImagePath = await SaveImageAsync(model.PrintImage, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        var order = new Order
        {
            OrderNumber = $"CO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            // This screen is for manually entered, in-person orders.
            Channel = OrderChannel.InPerson,
            Status = OrderStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow,
            DeliveryAt = DateTime.SpecifyKind(model.DeliveryAt, DateTimeKind.Local).ToUniversalTime(),
            CustomerName = model.CustomerName?.Trim(),
            CustomerPhone = model.CustomerPhone?.Trim(),
            OrderType = model.OrderType,
            CakeType = model.CakeType,
            RequestedWeightKg = model.RequestedWeightKg,
            PricePerKg = model.PricePerKg,
            PrintType = model.PrintType,
            BackgroundColor = model.BackgroundColor?.Trim(),
            MoldType = model.MoldType,
            HasBananaFilling = model.HasBananaFilling,
            HasWalnutFilling = model.HasWalnutFilling,
            WritingLocation = model.WritingLocation,
            CakeText = model.CakeText?.Trim(),
            DesignNotes = model.DesignNotes?.Trim(),
            SampleImagePath = sampleImagePath,
            PrintImagePath = printImagePath,
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
            PreviousStatus = OrderStatus.PendingReview,
            NewStatus = order.Status,
            ChangedAtUtc = DateTime.UtcNow,
            Note = "ثبت اولیه سفارش حضوری"
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

    private void ValidateImage(Microsoft.AspNetCore.Http.IFormFile? file, string key)
    {
        if (file is null || file.Length == 0)
            return;

        if (file.Length > MaxImageBytes)
            ModelState.AddModelError(key, "حجم هر تصویر حداکثر باید ۵ مگابایت باشد.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            ModelState.AddModelError(key, "فرمت تصویر باید JPG، PNG یا WebP باشد.");
    }

    private async Task<string?> SaveImageAsync(Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return null;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = Path.Combine(environment.WebRootPath, "uploads", "orders");
        Directory.CreateDirectory(folder);

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!IsSupportedImage(header, read, extension))
            throw new InvalidDataException("محتوای فایل انتخاب‌شده تصویر معتبر نیست.");

        stream.Position = 0;
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);
        await using (var output = System.IO.File.Create(fullPath))
            await stream.CopyToAsync(output, cancellationToken);

        return $"/uploads/orders/{fileName}";
    }

    private static bool IsSupportedImage(byte[] header, int length, string extension)
    {
        var jpeg = length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var webp = length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        return (jpeg && extension is ".jpg" or ".jpeg") || (png && extension == ".png") || (webp && extension == ".webp");
    }
}