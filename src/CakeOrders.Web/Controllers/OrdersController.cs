using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class OrdersController(CakeOrdersDbContext db, IWebHostEnvironment environment) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(
      string? search,
      OrderStatus? status,
      OrderChannel? channel,
      DateTime? deliveryFrom,
      DateTime? deliveryTo,
      int page = 1,
      CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;

        page = Math.Max(1, page);

        var query = db.Orders.AsNoTracking();

        // جست‌وجو بر اساس شماره سفارش، نام مشتری یا موبایل
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.OrderNumber.Contains(term) ||
                (x.CustomerName != null && x.CustomerName.Contains(term)) ||
                (x.CustomerPhone != null && x.CustomerPhone.Contains(term)));
        }

        // فیلتر وضعیت
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        // فیلتر کانال سفارش
        if (channel.HasValue)
            query = query.Where(x => x.Channel == channel.Value);

        // فیلتر تاریخ تحویل از
        if (deliveryFrom.HasValue)
        {
            var fromLocal = DateTime.SpecifyKind(
                deliveryFrom.Value.Date,
                DateTimeKind.Local);

            var fromUtc = fromLocal.ToUniversalTime();

            query = query.Where(x => x.DeliveryAt >= fromUtc);
        }

        // فیلتر تاریخ تحویل تا
        if (deliveryTo.HasValue)
        {
            var toLocalExclusive = DateTime.SpecifyKind(
                deliveryTo.Value.Date.AddDays(1),
                DateTimeKind.Local);

            var toUtcExclusive = toLocalExclusive.ToUniversalTime();

            query = query.Where(x => x.DeliveryAt < toUtcExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalPages > 0)
            page = Math.Min(page, totalPages);
        else
            page = 1;

        var orders = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var model = new OrderListViewModel
        {
            Search = search,
            Status = status,
            Channel = channel,
            DeliveryFrom = deliveryFrom,
            DeliveryTo = deliveryTo,
            Orders = orders,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(model);
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
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedByUserId = null,
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
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = "ثبت اولیه سفارش حضوری"
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"سفارش {order.OrderNumber} ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
    long id,
    CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        // دریافت شناسه کاربران ثبت‌شده در تاریخچه
        var userIds = order.StatusHistory
            .Where(x => !string.IsNullOrWhiteSpace(x.ChangedByUserId))
            .Select(x => x.ChangedByUserId!)
            .Distinct()
            .ToList();

        // دریافت نام نمایشی کاربران
        var userNames = await db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.DisplayName,
                cancellationToken);

        // دریافت تاریخچه تغییرات سفارش
        var changeLogs = await db.OrderChangeLogs
            .Where(x => x.OrderId == id)
            .OrderByDescending(x => x.ChangedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        // دریافت نام کاربران تغییر‌دهنده
        var changeLogUserIds = changeLogs
            .Where(x => !string.IsNullOrEmpty(x.ChangedByUserId))
            .Select(x => x.ChangedByUserId!)
            .Distinct()
            .ToList();

        var changeLogUserNames = await db.Users
            .Where(x => changeLogUserIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.DisplayName,
                cancellationToken);

        ViewBag.OrderChangeLogs = changeLogs;
        ViewBag.OrderChangeLogUserNames = changeLogUserNames;

        ViewBag.StatusHistoryUserNames = userNames;

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
    OrderStatusUpdateViewModel model,
    CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "اطلاعات ارسال‌شده معتبر نیست.";
            return RedirectToAction(nameof(Details), new { id = model.OrderId });
        }

        var order = await db.Orders
            .FirstOrDefaultAsync(x => x.Id == model.OrderId, cancellationToken);

        if (order is null)
            return NotFound();

        var allowedTransitions = order.Status switch
        {
            OrderStatus.PendingReview => new[]
            {
            OrderStatus.Confirmed,
            OrderStatus.Rejected,
            OrderStatus.Cancelled
        },

            OrderStatus.Confirmed => new[]
            {
            OrderStatus.InProduction,
            OrderStatus.Cancelled
        },

            OrderStatus.InProduction => new[]
            {
            OrderStatus.ReadyForDelivery,
            OrderStatus.Cancelled
        },

            OrderStatus.ReadyForDelivery => new[]
            {
            OrderStatus.Delivered
        },

            _ => Array.Empty<OrderStatus>()
        };

        if (!allowedTransitions.Contains(model.NewStatus))
        {
            TempData["Error"] = "تغییر وضعیت انتخاب‌شده مجاز نیست.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        if (model.NewStatus == OrderStatus.Rejected &&
            string.IsNullOrWhiteSpace(model.Note))
        {
            TempData["Error"] = "برای رد سفارش، ثبت دلیل الزامی است.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        order.Status = model.NewStatus;
        order.UpdatedByUserId = userId;

        if (model.NewStatus == OrderStatus.Rejected)
            order.RejectionReason = model.Note!.Trim();

        db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = model.NewStatus,
            ChangedAtUtc = now,
            ChangedByUserId = userId,
            Note = model.Note?.Trim()
        });

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "وضعیت سفارش با موفقیت تغییر کرد.";

        return RedirectToAction(nameof(Details), new { id = order.Id });
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

    [HttpGet]
    public async Task<IActionResult> Edit(
    long id,
    CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        var model = new OrderEditViewModel
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            OrderType = order.OrderType,
            CakeType = order.CakeType,
            RequestedWeightKg = order.RequestedWeightKg,
            PricePerKg = order.PricePerKg,
            PrintType = order.PrintType,
            BackgroundColor = order.BackgroundColor,
            MoldType = order.MoldType,
            HasBananaFilling = order.HasBananaFilling,
            HasWalnutFilling = order.HasWalnutFilling,
            WritingLocation = order.WritingLocation,
            CakeText = order.CakeText,
            DesignNotes = order.DesignNotes,
            CurrentSampleImagePath = order.SampleImagePath,
            CurrentPrintImagePath = order.PrintImagePath,
            MacaronCost = order.MacaronCost,
            FlowerCost = order.FlowerCost,
            DummyCost = order.DummyCost,
            PrintingCost = order.PrintingCost,
            ShippingCost = order.ShippingCost,
            OtherCost = order.OtherCost,
            Discount = order.Discount,
            DepositAmount = order.DepositAmount,
            ReferrerCode = order.ReferrerCode,
            DeliveryAt = DateTime.SpecifyKind(order.DeliveryAt, DateTimeKind.Utc).ToLocalTime()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
    long id,
    OrderEditViewModel model,
    CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        var order = await db.Orders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        // اعتبارسنجی مبالغ
        if (model.CalculatedTotal < 0)
        {
            ModelState.AddModelError(
                nameof(model.Discount),
                "تخفیف نمی‌تواند از جمع هزینه‌ها بیشتر باشد.");
        }

        if (model.DepositAmount > Math.Max(0, model.CalculatedTotal))
        {
            ModelState.AddModelError(
                nameof(model.DepositAmount),
                "بیعانه نمی‌تواند از مبلغ نهایی بیشتر باشد.");
        }

        // اعتبارسنجی تصاویر جدید
        ValidateImage(model.SampleImage, nameof(model.SampleImage));
        ValidateImage(model.PrintImage, nameof(model.PrintImage));

        // مسیر تصاویر فعلی برای نمایش مجدد فرم
        model.CurrentSampleImagePath = order.SampleImagePath;
        model.CurrentPrintImagePath = order.PrintImagePath;

        if (!ModelState.IsValid)
            return View(model);

        string? newSampleImagePath = null;
        string? newPrintImagePath = null;

        try
        {
            newSampleImagePath = await SaveImageAsync(
                model.SampleImage,
                cancellationToken);

            newPrintImagePath = await SaveImageAsync(
                model.PrintImage,
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            DeleteOrderImage(newSampleImagePath);
            DeleteOrderImage(newPrintImagePath);

            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch
        {
            DeleteOrderImage(newSampleImagePath);
            DeleteOrderImage(newPrintImagePath);
            throw;
        }

        // مسیر تصاویر قبلی را نگه می‌داریم تا پس از ذخیره موفق حذف شوند.
        var oldSampleImagePath = order.SampleImagePath;
        var oldPrintImagePath = order.PrintImagePath;

        // تبدیل زمان محلی به UTC
        var deliveryAtUtc = DateTime.SpecifyKind(
            model.DeliveryAt,
            DateTimeKind.Local
        ).ToUniversalTime();

        var changedAtUtc = DateTime.UtcNow;
        var changedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        void AddChangeLog(string fieldName, object? oldValue, object? newValue)
        {
            var oldText = FormatChangeLogValue(oldValue);
            var newText = FormatChangeLogValue(newValue);

            if (string.Equals(oldText, newText, StringComparison.Ordinal))
                return;

            db.OrderChangeLogs.Add(new OrderChangeLog
            {
                OrderId = order.Id,
                FieldName = fieldName,
                OldValue = oldText,
                NewValue = newText,
                ChangedAtUtc = changedAtUtc,
                ChangedByUserId = changedByUserId
            });
        }

        // اطلاعات پایه
        AddChangeLog("نام مشتری", order.CustomerName, model.CustomerName?.Trim());
        AddChangeLog("شماره تماس", order.CustomerPhone, model.CustomerPhone?.Trim());
        AddChangeLog("نوع سفارش", order.OrderType, model.OrderType);
        AddChangeLog("نوع کیک", order.CakeType, model.CakeType);
        AddChangeLog("زمان تحویل", order.DeliveryAt, deliveryAtUtc);

        // مشخصات کیک
        AddChangeLog("وزن درخواستی", order.RequestedWeightKg, model.RequestedWeightKg);
        AddChangeLog("قیمت هر کیلو", order.PricePerKg, model.PricePerKg);
        AddChangeLog("نوع چاپ", order.PrintType, model.PrintType);
        AddChangeLog("رنگ زمینه", order.BackgroundColor, model.BackgroundColor?.Trim());
        AddChangeLog("نوع قالب", order.MoldType, model.MoldType);
        AddChangeLog("فیلینگ موز", order.HasBananaFilling, model.HasBananaFilling);
        AddChangeLog("فیلینگ گردو", order.HasWalnutFilling, model.HasWalnutFilling);
        AddChangeLog("محل نوشته", order.WritingLocation, model.WritingLocation);
        AddChangeLog("متن روی کیک", order.CakeText, model.CakeText?.Trim());
        AddChangeLog("توضیحات طرح", order.DesignNotes, model.DesignNotes?.Trim());

        // تصاویر
        if (newSampleImagePath is not null)
        {
            AddChangeLog(
                "تصویر نمونه کیک",
                string.IsNullOrWhiteSpace(oldSampleImagePath) ? "ثبت نشده" : "تصویر قبلی",
                "تصویر جدید");
        }

        if (newPrintImagePath is not null)
        {
            AddChangeLog(
                "تصویر چاپی",
                string.IsNullOrWhiteSpace(oldPrintImagePath) ? "ثبت نشده" : "تصویر قبلی",
                "تصویر جدید");
        }

        // هزینه‌ها و پرداخت
        AddChangeLog("هزینه ماکارون", order.MacaronCost, model.MacaronCost);
        AddChangeLog("هزینه گل", order.FlowerCost, model.FlowerCost);
        AddChangeLog("هزینه ماکت", order.DummyCost, model.DummyCost);
        AddChangeLog("هزینه چاپ", order.PrintingCost, model.PrintingCost);
        AddChangeLog("هزینه ارسال", order.ShippingCost, model.ShippingCost);
        AddChangeLog("سایر هزینه‌ها", order.OtherCost, model.OtherCost);
        AddChangeLog("تخفیف", order.Discount, model.Discount);
        AddChangeLog("مبلغ نهایی", order.FinalAmount, model.CalculatedTotal);
        AddChangeLog("بیعانه", order.DepositAmount, model.DepositAmount);
        AddChangeLog("کد معرف", order.ReferrerCode, model.ReferrerCode?.Trim());

        // به‌روزرسانی اطلاعات قابل ویرایش
        order.DeliveryAt = deliveryAtUtc;

        order.CustomerName = model.CustomerName?.Trim();
        order.CustomerPhone = model.CustomerPhone?.Trim();

        order.OrderType = model.OrderType;
        order.CakeType = model.CakeType;

        order.RequestedWeightKg = model.RequestedWeightKg;
        order.PricePerKg = model.PricePerKg;

        order.PrintType = model.PrintType;
        order.BackgroundColor = model.BackgroundColor?.Trim();
        order.MoldType = model.MoldType;

        order.HasBananaFilling = model.HasBananaFilling;
        order.HasWalnutFilling = model.HasWalnutFilling;

        order.WritingLocation = model.WritingLocation;
        order.CakeText = model.CakeText?.Trim();
        order.DesignNotes = model.DesignNotes?.Trim();

        // فقط در صورت انتخاب تصویر جدید، مسیر تصویر تغییر می‌کند.
        if (newSampleImagePath is not null)
            order.SampleImagePath = newSampleImagePath;

        if (newPrintImagePath is not null)
            order.PrintImagePath = newPrintImagePath;

        order.MacaronCost = model.MacaronCost;
        order.FlowerCost = model.FlowerCost;
        order.DummyCost = model.DummyCost;
        order.PrintingCost = model.PrintingCost;
        order.ShippingCost = model.ShippingCost;
        order.OtherCost = model.OtherCost;

        order.Discount = model.Discount;
        order.FinalAmount = model.CalculatedTotal;
        order.DepositAmount = model.DepositAmount;

        order.ReferrerCode = model.ReferrerCode?.Trim();

        order.UpdatedByUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // اگر ذخیره اطلاعات سفارش شکست خورد، تصاویر جدید حذف شوند.
            DeleteOrderImage(newSampleImagePath);
            DeleteOrderImage(newPrintImagePath);
            throw;
        }

        // پس از ذخیره موفق، تصاویر قبلی حذف می‌شوند.
        if (newSampleImagePath is not null)
            DeleteOrderImage(oldSampleImagePath);

        if (newPrintImagePath is not null)
            DeleteOrderImage(oldPrintImagePath);

        TempData["Success"] = "اطلاعات سفارش با موفقیت ویرایش شد.";

        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    private void DeleteOrderImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        const string allowedFolder = "/uploads/orders/";

        if (!imagePath.StartsWith(
            allowedFolder,
            StringComparison.OrdinalIgnoreCase))
            return;

        var fileName = Path.GetFileName(imagePath);

        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var fullPath = Path.Combine(
            environment.WebRootPath,
            "uploads",
            "orders",
            fileName);

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private static string? FormatChangeLogValue(object? value)
    {
        if (value is null)
            return null;

        if (value is DateTime dateTime)
        {
            var utcDateTime = DateTime.SpecifyKind(
                dateTime,
                DateTimeKind.Utc);

            return utcDateTime
                .ToLocalTime()
                .ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (value is bool boolean)
            return boolean ? "بله" : "خیر";

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString();
    }
}