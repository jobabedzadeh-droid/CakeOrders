using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels.Orders;
using CakeOrders.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class OrdersController(CakeOrdersDbContext db, IWebHostEnvironment environment, IOrderProductionRoutingService productionRouting, ILogger<OrdersController> logger) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(
      string? search,
      OrderStatus? status,
      OrderChannel? channel,
      OrderKind? kind,
      DateTime? deliveryFrom,
      DateTime? deliveryTo,
      int page = 1,
      CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;

        page = Math.Max(1, page);

        var query = db.Orders.AsNoTracking();

        if (!User.IsInRole("Admin"))
        {
            var workshopId = await GetCurrentWorkshopIdAsync(cancellationToken);
            if (workshopId.HasValue)
                query = query.Where(x => x.ProductionTasks.Any(t => t.WorkshopId == workshopId.Value));
            else
                query = query.Where(x => false);
        }

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

        // فیلتر نوع سفارش
        if (kind.HasValue)
            query = query.Where(x => x.Kind == kind.Value);

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
            Kind = kind,
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
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CreateBirthdayCake(
    CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        var model = new OrderCreateViewModel();

        await PopulateProductionBranchesAsync(model, cancellationToken);

        return View(model);
    }

    private async Task PopulateProductionBranchesAsync(
        OrderCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.DraftImageBaseUrl = GetDraftImageBaseUrl();
        model.ProductionBranches = await db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name,
                Selected = x.Id == model.ProductionBranchId
            })
            .ToListAsync(cancellationToken);
    }

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
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadDraftImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, message = "فایلی برای بارگذاری انتخاب نشده است." });
        if (file.Length > MaxImageBytes)
            return BadRequest(new { success = false, message = "حجم هر تصویر حداکثر باید ۵ مگابایت باشد." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { success = false, message = "فرمت تصویر باید JPG، PNG یا WebP باشد." });

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!IsSupportedImage(header, read, extension))
            return BadRequest(new { success = false, message = "محتوای فایل انتخاب‌شده تصویر معتبر نیست." });

        var folder = GetDraftImageFolderPath();
        Directory.CreateDirectory(folder);
        var token = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, token);
        input.Position = 0;
        await using (var output = System.IO.File.Create(fullPath))
            await input.CopyToAsync(output, cancellationToken);

        return Json(new
        {
            success = true,
            token,
            url = GetDraftImageBaseUrl() + token
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> CreateBirthdayCake(OrderCreateViewModel model, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        Customer? selectedCustomer = null;

        if (!model.CustomerId.HasValue || model.CustomerId.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "ابتدا مشتری را با جست‌وجوی نام، نام خانوادگی یا شماره موبایل انتخاب کنید.");
        }
        else
        {
            selectedCustomer = await db.Customers
                .FirstOrDefaultAsync(x => x.Id == model.CustomerId.Value && x.IsActive, cancellationToken);

            if (selectedCustomer is null)
            {
                ModelState.AddModelError(nameof(model.CustomerId), "مشتری انتخاب‌شده معتبر یا فعال نیست؛ دوباره جست‌وجو و انتخاب کنید.");
                selectedCustomer = null;
            }
            else if ((!string.IsNullOrWhiteSpace(selectedCustomer.Phone) &&
                      (!CustomerPhoneNormalizer.TryNormalize(model.CustomerPhone, out var normalizedPhone) ||
                       !string.Equals(normalizedPhone, selectedCustomer.NormalizedPhone, StringComparison.Ordinal))) ||
                     (string.IsNullOrWhiteSpace(selectedCustomer.Phone) && !string.IsNullOrWhiteSpace(model.CustomerPhone)))
            {
                ModelState.AddModelError(nameof(model.CustomerPhone), "شماره موبایل با مشتری انتخاب‌شده مطابقت ندارد؛ دوباره مشتری را جست‌وجو و انتخاب کنید.");
                selectedCustomer = null;
            }
            else
            {
                model.CustomerName = selectedCustomer.FullName;
                model.CustomerFirstName = selectedCustomer.FirstName;
                model.CustomerLastName = selectedCustomer.LastName;
                model.CustomerPhone = string.IsNullOrWhiteSpace(selectedCustomer.Phone) ? string.Empty : selectedCustomer.Phone;
                ModelState.Remove(nameof(model.CustomerName));
                ModelState.Remove(nameof(model.CustomerFirstName));
                ModelState.Remove(nameof(model.CustomerLastName));
                ModelState.Remove(nameof(model.CustomerPhone));
            }
        }

        // Every new order must already have all Rahkaran sales-request prerequisites.
        // This prevents an order from entering the workflow with incomplete remote mappings.
        var rahkaranSettings = await db.RahkaranIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (selectedCustomer is not null)
        {
            if (!long.TryParse(selectedCustomer.RahkaranCustomerId, out var rahkaranCustomerId) ||
                rahkaranCustomerId <= 0)
            {
                ModelState.AddModelError(nameof(model.CustomerId),
                    "مشتری انتخاب‌شده باید ابتدا با مشتری متناظر در راهکاران تطبیق داده شود.");
            }

            if (string.IsNullOrWhiteSpace(selectedCustomer.RahkaranDLCode))
            {
                ModelState.AddModelError(nameof(model.CustomerId),
                    "کد تفصیلی مشتری در راهکاران موجود نیست؛ اطلاعات مشتری را همگام‌سازی و تکمیل کنید.");
            }
        }

        if (rahkaranSettings is null)
        {
            ModelState.AddModelError(string.Empty,
                "تنظیمات درخواست فروش راهکاران هنوز ثبت نشده است؛ ابتدا نگاشت‌های راهکاران را تکمیل کنید.");
        }
        else
        {
            if (rahkaranSettings.SalesOfficeId is not > 0)
                ModelState.AddModelError(string.Empty, "مرکز فروش راهکاران تعیین نشده است.");
            if (rahkaranSettings.SalesAreaId is not > 0)
                ModelState.AddModelError(string.Empty, "حوزه فروش راهکاران تعیین نشده است.");
            if (rahkaranSettings.SalesTypeId is not > 0)
                ModelState.AddModelError(string.Empty, "نحوه فروش راهکاران تعیین نشده است.");
            if (rahkaranSettings.SalesInventoryId is not > 0)
                ModelState.AddModelError(string.Empty, "انبار راهکاران تعیین نشده است.");
            if (rahkaranSettings.SalesPlantId is not > 0)
                ModelState.AddModelError(string.Empty, "مرکز نگهداری راهکاران تعیین نشده است.");
            if (rahkaranSettings.SalesCurrencyId is not > 0)
                ModelState.AddModelError(string.Empty, "ارز درخواست فروش راهکاران تعیین نشده است.");

            // Deposit receipts are also posted to Rahkaran. Keep both supported
            // payment routes fully configured before accepting a new order.
            if (rahkaranSettings.BranchId is not > 0)
                ModelState.AddModelError(string.Empty, "شعبه مالی راهکاران تعیین نشده است.");
            if (rahkaranSettings.CashId is not > 0)
                ModelState.AddModelError(string.Empty, "صندوق راهکاران تعیین نشده است.");
            if (rahkaranSettings.CashAccountingOperationId is not > 0)
                ModelState.AddModelError(string.Empty, "عملیات حسابداری دریافت نقدی راهکاران تعیین نشده است.");
            if (rahkaranSettings.CashFlowFactorId is not > 0)
                ModelState.AddModelError(string.Empty, "عامل گردش نقدینگی دریافت نقدی راهکاران تعیین نشده است.");
            if (rahkaranSettings.PosBankAccountId is not > 0)
                ModelState.AddModelError(string.Empty, "حساب بانکی مقصد کارت‌خوان در راهکاران تعیین نشده است.");
            if (rahkaranSettings.PosAccountingOperationId is not > 0)
                ModelState.AddModelError(string.Empty, "عملیات حسابداری کارت‌خوان راهکاران تعیین نشده است.");
            if (rahkaranSettings.PosCashFlowFactorId is not > 0)
                ModelState.AddModelError(string.Empty, "عامل گردش نقدینگی کارت‌خوان راهکاران تعیین نشده است.");

            if (rahkaranSettings.SettlementCashAccountingOperationId is not > 0)
                ModelState.AddModelError(string.Empty, "عملیات حسابداری تسویه نقدی راهکاران تعیین نشده است.");
            if (rahkaranSettings.SettlementCashFlowFactorId is not > 0)
                ModelState.AddModelError(string.Empty, "عامل گردش نقدینگی تسویه نقدی راهکاران تعیین نشده است.");
            if (rahkaranSettings.SettlementPosBankAccountId is not > 0)
                ModelState.AddModelError(string.Empty, "حساب بانکی مقصد تسویه کارت‌خوان راهکاران تعیین نشده است.");
            if (rahkaranSettings.SettlementPosAccountingOperationId is not > 0)
                ModelState.AddModelError(string.Empty, "عملیات حسابداری تسویه کارت‌خوان راهکاران تعیین نشده است.");
            if (rahkaranSettings.SettlementPosCashFlowFactorId is not > 0)
                ModelState.AddModelError(string.Empty, "عامل گردش نقدینگی تسویه کارت‌خوان راهکاران تعیین نشده است.");

            var (rahkaranProductId, rahkaranUnitId) = model.CakeType switch
            {
                CakeType.Cream => (rahkaranSettings.CreamCakeProductId, rahkaranSettings.CreamCakeUnitId),
                CakeType.SemiFondant => (rahkaranSettings.SemiFondantCakeProductId, rahkaranSettings.SemiFondantCakeUnitId),
                CakeType.FullFondant => (rahkaranSettings.FullFondantCakeProductId, rahkaranSettings.FullFondantCakeUnitId),
                _ => (null, null)
            };

            if (rahkaranProductId is not > 0)
                ModelState.AddModelError(nameof(model.CakeType), "محصول متناظر این نوع کیک در راهکاران تعیین نشده است.");
            if (rahkaranUnitId is not > 0)
                ModelState.AddModelError(nameof(model.CakeType), "واحد سنجش کیلوگرم این محصول در راهکاران تعیین نشده است.");
        }

        var selectedBranchExists = await db.Branches.AnyAsync(
            x => x.Id == model.ProductionBranchId && x.IsActive,
            cancellationToken);
        if (!selectedBranchExists)
            ModelState.AddModelError(nameof(model.ProductionBranchId), "شعبه انتخاب‌شده معتبر یا فعال نیست.");

        var cakeWorkshopExists = await db.Workshops.AnyAsync(
            x => x.BranchId == model.ProductionBranchId &&
                 x.IsActive &&
                 x.Name == "کیک‌سازی",
            cancellationToken);

        if (!cakeWorkshopExists)
        {
            ModelState.AddModelError(
                nameof(model.ProductionBranchId),
                "برای شعبه انتخاب‌شده کارگاه فعال «کیک‌سازی» تعریف نشده است.");
        }

        if (model.RequestedDepositAmount > Math.Max(0, model.CalculatedTotal))
            ModelState.AddModelError(nameof(model.RequestedDepositAmount), "بیعانه درخواستی نمی‌تواند از مبلغ نهایی بیشتر باشد.");

        long? referrerId = null;
        if (!string.IsNullOrWhiteSpace(model.ReferrerCode))
        {
            referrerId = await db.Referrers.AsNoTracking()
                .Where(x => x.IsActive && x.Code == model.ReferrerCode.Trim())
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!referrerId.HasValue)
                ModelState.AddModelError(nameof(model.ReferrerCode), "کد معرف معتبر نیست یا در فهرست کارکنان وجود ندارد.");
        }

        ValidateImage(model.SampleImage, nameof(model.SampleImage));
        ValidateImage(model.PrintImage, nameof(model.PrintImage));

        if (!ModelState.IsValid)
        {
            await PopulateProductionBranchesAsync(model, cancellationToken);
            return View(model);
        }

        string? sampleImagePath = null;
        string? printImagePath = null;
        try
        {
            sampleImagePath = await PromoteDraftImageAsync(model.SampleImageDraftToken, cancellationToken)
                ?? await SaveImageAsync(model.SampleImage, cancellationToken);
            printImagePath = await PromoteDraftImageAsync(model.PrintImageDraftToken, cancellationToken)
                ?? await SaveImageAsync(model.PrintImage, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateProductionBranchesAsync(model, cancellationToken);
            return View(model);
        }

        var order = new Order
        {
            OrderNumber = $"CO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Kind = OrderKind.BirthdayCake,
            // This screen is for manually entered, in-person orders.
            Channel = OrderChannel.InPerson,
            // No order is auto-confirmed at creation; confirmation follows a successful deposit or an authorized manual decision.
            Status = OrderStatus.PendingReview,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedByUserId = null,
            CreatedAtUtc = DateTime.UtcNow,
            ProductionBranchId = model.ProductionBranchId,
            DeliveryAt = DateTime.SpecifyKind(model.DeliveryAt, DateTimeKind.Local).ToUniversalTime(),
            CustomerId = selectedCustomer!.Id,
            CustomerName = selectedCustomer.FullName,
            CustomerPhone = selectedCustomer.Phone,
            RahkaranCustomerId = selectedCustomer.RahkaranCustomerId,
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
            FinalAmount = model.CalculatedTotal,
            RequestedDepositAmount = model.RequestedDepositAmount,
            DepositAmount = 0,
            ReferrerCode = model.ReferrerCode?.Trim(),
            ReferrerId = referrerId
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = OrderStatus.PendingReview,
            NewStatus = order.Status,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = model.RequestedDepositAmount > 0
                ? "ثبت سفارش؛ در انتظار پرداخت بیعانه و تأیید خودکار پس از تکمیل آن"
                : "ثبت اولیه سفارش؛ در انتظار بررسی و تأیید دستی"
        });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        if (order.Status == OrderStatus.Confirmed &&
            !await productionRouting.EnsureBirthdayCakeTaskAsync(
                order,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                DateTime.UtcNow,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            DeleteOrderImage(sampleImagePath);
            DeleteOrderImage(printImagePath);

            ModelState.AddModelError(
                nameof(model.ProductionBranchId),
                "ایجاد وظیفه تولید ممکن نشد؛ کارگاه فعال کیک‌سازی شعبه را بررسی کنید.");

            await PopulateProductionBranchesAsync(model, cancellationToken);
            return View(model);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = order.Status == OrderStatus.Confirmed
            ? $"سفارش {order.OrderNumber} ثبت و به کارگاه کیک‌سازی ارجاع شد."
            : $"سفارش {order.OrderNumber} ثبت شد و پس از تأیید به تولید ارجاع می‌شود.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
    long id,
    CancellationToken cancellationToken)
    {
        var workshopIdForDetails = User.IsInRole("Admin") ? null : await GetCurrentWorkshopIdAsync(cancellationToken);
        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.StatusHistory)
            .Include(x => x.Referrer)
            .Include(x => x.Items)
                .ThenInclude(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == id &&
                (User.IsInRole("Admin") || (workshopIdForDetails.HasValue && x.ProductionTasks.Any(t => t.WorkshopId == workshopIdForDetails.Value))), cancellationToken);

        if (order is null)
            return NotFound();

        ViewBag.IsWorkshopMember = !User.IsInRole("Admin") && workshopIdForDetails.HasValue;

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

        var cashPayments = await db.OrderCashPayments
            .AsNoTracking()
            .Where(x => x.OrderId == id)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var cashReceiverIds = cashPayments
            .Select(x => x.ReceivedByUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var cashReceiverNames = await db.Users
            .AsNoTracking()
            .Where(x => cashReceiverIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        ViewBag.RahkaranSalesRequestOutbox = await db.RahkaranSalesRequestOutbox
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == id, cancellationToken);
        ViewBag.RahkaranReceiptOutboxes = await db.RahkaranReceiptOutbox
            .AsNoTracking()
            .Where(x => x.OrderId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        ViewBag.CashPayments = cashPayments;
        ViewBag.CashReceiverNames = cashReceiverNames;
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

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // قفل سفارش از تأیید هم‌زمان و ایجاد وظیفه تولید تکراری جلوگیری می‌کند.
        var order = await db.Orders
            .FromSqlInterpolated(
                $"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {model.OrderId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
            return NotFound();

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actor = await db.Users.AsNoTracking()
            .Where(x => x.Id == actorUserId)
            .Select(x => new { x.CanManuallyConfirmOrders, x.WorkshopId })
            .SingleOrDefaultAsync(cancellationToken);

        if (actor?.WorkshopId is not null)
            return Forbid();

        if ((model.NewStatus == OrderStatus.Confirmed || model.NewStatus == OrderStatus.Rejected) &&
            string.IsNullOrWhiteSpace(model.Note))
        {
            TempData["Error"] = "برای تأیید یا رد سفارش، ثبت دلیل الزامی است.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        if (model.NewStatus == OrderStatus.Confirmed && order.DepositAmount < order.RequestedDepositAmount)
        {
            var hasValidReferrer = !string.IsNullOrWhiteSpace(order.ReferrerCode) &&
                await db.Referrers.AnyAsync(x => x.IsActive && x.Code == order.ReferrerCode.Trim(), cancellationToken);
            var partiallyPaid = order.DepositAmount > 0;
            var referralWithoutDeposit = order.DepositAmount == 0 && hasValidReferrer;

            if (!User.IsInRole("Admin") &&
                (actor?.CanManuallyConfirmOrders != true || (!partiallyPaid && !referralWithoutDeposit)))
            {
                TempData["Error"] = "تأیید دستی این سفارش مجاز نیست؛ پرداخت کامل بیعانه یا مجوز تأیید دستی همراه با شرایط معتبر لازم است.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            if (order.DepositAmount == 0 && !referralWithoutDeposit)
            {
                TempData["Error"] = "سفارش بدون بیعانه فقط در صورت داشتن معرف معتبر قابل تأیید دستی است.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
        }

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

        var now = DateTime.UtcNow;
        var userId = actorUserId;

        if (model.NewStatus == OrderStatus.Confirmed &&
            order.Kind == OrderKind.BirthdayCake &&
            !await productionRouting.EnsureBirthdayCakeTaskAsync(order, userId, now, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Error"] = "کارگاه فعال «کیک‌سازی» برای شعبه سفارش پیدا نشد؛ وضعیت سفارش تغییر نکرد.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        var previousStatus = order.Status;
        order.Status = model.NewStatus;
        order.UpdatedByUserId = userId;

        if (model.NewStatus == OrderStatus.Confirmed)
        {
            await RahkaranSalesRequestOutboxEnqueuer.EnqueueIfMissingAsync(
                db, order.Id, now, cancellationToken);
        }

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
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = model.NewStatus == OrderStatus.Confirmed &&
                              order.Kind == OrderKind.BirthdayCake
            ? "سفارش تأیید و وظیفه تولید آن به کارگاه کیک‌سازی ارجاع شد."
            : "وضعیت سفارش با موفقیت تغییر کرد.";

        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    private string GetDraftImageFolderName()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))[..16];
    }

    private string GetDraftImageFolderPath() =>
        Path.Combine(environment.WebRootPath, "uploads", "order-drafts", GetDraftImageFolderName());

    private string GetDraftImageBaseUrl() =>
        $"/uploads/order-drafts/{GetDraftImageFolderName()}/";

    private async Task<string?> PromoteDraftImageAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var fileName = Path.GetFileName(token);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!string.Equals(fileName, token, StringComparison.Ordinal) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "N", out _) ||
            extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new InvalidDataException("شناسه تصویر بارگذاری‌شده معتبر نیست؛ تصویر را دوباره انتخاب کنید.");

        var source = Path.Combine(GetDraftImageFolderPath(), fileName);
        if (!System.IO.File.Exists(source))
            throw new InvalidDataException("تصویر موقت پیدا نشد یا منقضی شده است؛ تصویر را دوباره انتخاب کنید.");

        var destinationFolder = Path.Combine(environment.WebRootPath, "uploads", "orders");
        Directory.CreateDirectory(destinationFolder);
        var destinationName = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(destinationFolder, destinationName);

        await using (var input = System.IO.File.OpenRead(source))
        {
            var header = new byte[12];
            var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (!IsSupportedImage(header, read, extension))
                throw new InvalidDataException("محتوای تصویر بارگذاری‌شده معتبر نیست؛ تصویر را دوباره انتخاب کنید.");
        }

        System.IO.File.Move(source, destination);
        return $"/uploads/orders/{destinationName}";
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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Kind != OrderKind.BirthdayCake)
        {
            return BadRequest("این سفارش از نوع کیک تولد نیست.");
        }

        var model = new OrderEditViewModel
        {
            Id = order.Id,
            ProductionBranchId = order.ProductionBranchId ?? 0,
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
            RequestedDepositAmount = order.RequestedDepositAmount,
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
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        if (id != model.Id)
            return BadRequest();

        // ProductionBranchId is required for creating an order, but is not editable here.
        // Keep the persisted value unchanged and do not validate the hidden edit-form value.
        ModelState.Remove(nameof(model.ProductionBranchId));

        var order = await db.Orders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        if (order.Kind != OrderKind.BirthdayCake)
            return BadRequest("این سفارش از نوع کیک تولد نیست.");

        // مبلغ نهایی باید حداقل برابر مجموع پرداخت‌های موفق باشد.
        if (model.CalculatedTotal < order.PaidAmount)
        {
            ModelState.AddModelError(
                nameof(model.PricePerKg),
                "مبلغ نهایی نمی‌تواند از مجموع پرداخت‌های موفق سفارش کمتر باشد.");
        }

        // بیعانه درخواستی باید حداقل برابر بیعانه پرداخت‌شده و حداکثر برابر مبلغ نهایی باشد.
        if (model.RequestedDepositAmount < order.DepositAmount ||
            model.RequestedDepositAmount > model.CalculatedTotal)
        {
            ModelState.AddModelError(
                nameof(model.RequestedDepositAmount),
                "بیعانه درخواستی نمی‌تواند از بیعانه پرداخت‌شده کمتر یا از مبلغ نهایی بیشتر باشد.");
        }

        long? referrerId = null;
        if (!string.IsNullOrWhiteSpace(model.ReferrerCode))
        {
            referrerId = await db.Referrers.AsNoTracking()
                .Where(x => x.IsActive && x.Code == model.ReferrerCode.Trim())
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!referrerId.HasValue)
                ModelState.AddModelError(nameof(model.ReferrerCode), "کد معرف معتبر نیست یا در فهرست کارکنان وجود ندارد.");
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
        AddChangeLog("مبلغ نهایی", order.FinalAmount, model.CalculatedTotal);
        AddChangeLog("بیعانه درخواستی", order.RequestedDepositAmount, model.RequestedDepositAmount);
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

        order.FinalAmount = model.CalculatedTotal;
        order.RequestedDepositAmount = model.RequestedDepositAmount;

        order.ReferrerCode = model.ReferrerCode?.Trim();
        order.ReferrerId = referrerId;

        order.UpdatedByUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var latestProductionTaskStatus = await db.ProductionTasks
                .Where(x => x.OrderId == order.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => (ProductionTaskStatus?)x.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestProductionTaskStatus == ProductionTaskStatus.Rejected &&
                !await productionRouting.EnsureBirthdayCakeTaskAsync(
                    order, changedByUserId, DateTime.UtcNow, cancellationToken))
            {
                DeleteOrderImage(newSampleImagePath);
                DeleteOrderImage(newPrintImagePath);
                ModelState.AddModelError(string.Empty, "کارگاه فعال کیک‌سازی برای ارجاع مجدد سفارش پیدا نشد.");
                return View(model);
            }

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

    private async Task<long?> GetCurrentWorkshopIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => x.WorkshopId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsWorkshopMemberAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return false;

        return await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == User.FindFirstValue(ClaimTypes.NameIdentifier) &&
                           x.IsActive && x.WorkshopId != null,
                cancellationToken);
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