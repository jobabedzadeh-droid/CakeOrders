using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using CakeOrders.Web.ViewModels.Integrations.Rahkaran;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RahkaranSettingsController(
    CakeOrdersDbContext db,
    IRahkaranMasterDataClient rahkaran,
    IOptions<RahkaranOptions> options,
    ILogger<RahkaranSettingsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await db.RahkaranIntegrationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        var model = new RahkaranSettingsViewModel
        {
            BranchId = settings?.BranchId,
            CashId = settings?.CashId,
            CashAccountingOperationId = settings?.CashAccountingOperationId,
            CashFlowFactorId = settings?.CashFlowFactorId,
            PosBankAccountId = settings?.PosBankAccountId,
            PosAccountingOperationId = settings?.PosAccountingOperationId,
            PosCashFlowFactorId = settings?.PosCashFlowFactorId,
            SalesOfficeId = settings?.SalesOfficeId,
            SalesAreaId = settings?.SalesAreaId,
            SalesTypeId = settings?.SalesTypeId,
            SalesInventoryId = settings?.SalesInventoryId,
            SalesPlantId = settings?.SalesPlantId,
            SalesCurrencyId = settings?.SalesCurrencyId,
            SalesPaymentAgreementId = settings?.SalesPaymentAgreementId,
            CreamCakeProductId = settings?.CreamCakeProductId,
            CreamCakeUnitId = settings?.CreamCakeUnitId,
            SemiFondantCakeProductId = settings?.SemiFondantCakeProductId,
            SemiFondantCakeUnitId = settings?.SemiFondantCakeUnitId,
            FullFondantCakeProductId = settings?.FullFondantCakeProductId,
            FullFondantCakeUnitId = settings?.FullFondantCakeUnitId,
            ServiceBaseUrl = options.Value.ServiceBaseUrl,
            IntegrationEnabled = options.Value.Enabled,
            CachedEmployeeCount = await db.Referrers.CountAsync(cancellationToken)
        };

        await LoadMasterDataAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncEmployees(CancellationToken cancellationToken)
    {
        var phase = "دریافت فهرست کارکنان";

        try
        {
            logger.LogInformation("Rahkaran employee synchronization started.");

            var receivedEmployees = await rahkaran.GetEmployeesAsync(cancellationToken);
            var employeesWithCode = receivedEmployees
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .ToList();

            var employees = employeesWithCode
                .GroupBy(x => x.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            logger.LogInformation(
                "Rahkaran employee response processed. Received={ReceivedCount}, WithPersonnelCode={WithCodeCount}, UniquePersonnelCodes={UniqueCodeCount}.",
                receivedEmployees.Count,
                employeesWithCode.Count,
                employees.Count);

            if (employees.Count == 0)
            {
                logger.LogWarning(
                    "Rahkaran employee synchronization stopped because no employee with a valid personnel code was returned. Received={ReceivedCount}.",
                    receivedEmployees.Count);

                TempData["Error"] = "فهرست کارکنان از راهکاران خالی بود یا کد پرسنلی معتبری در پاسخ وجود نداشت؛ اطلاعات داخلی تغییری نکرد.";
                return RedirectToAction(nameof(Index));
            }

            phase = "خواندن کارکنان موجود از پایگاه داده";
            var existingReferrers = await db.Referrers.ToListAsync(cancellationToken);

            var duplicateExistingCodes = existingReferrers
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .Count(group => group.Count() > 1);

            if (duplicateExistingCodes > 0)
            {
                logger.LogWarning(
                    "Duplicate personnel codes already exist in the local Referrers table. DuplicateGroups={DuplicateGroupCount}. The first record per code will be used for matching; existing records will not be deleted.",
                    duplicateExistingCodes);
            }

            var referrersByCode = existingReferrers
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            logger.LogInformation(
                "Local referrers loaded for synchronization. ExistingRecords={ExistingCount}, DistinctCodes={DistinctCodeCount}.",
                existingReferrers.Count,
                referrersByCode.Count);

            phase = "به‌روزرسانی اطلاعات کارکنان";
            var addedCount = 0;
            var updatedCount = 0;

            foreach (var employee in employees)
            {
                var code = employee.Code!.Trim();
                var firstName = employee.FirstName?.Trim() ?? string.Empty;
                var lastName = employee.LastName?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(firstName) &&
                    string.IsNullOrWhiteSpace(lastName) &&
                    !string.IsNullOrWhiteSpace(employee.Name))
                {
                    firstName = employee.Name.Trim();
                }

                if (!referrersByCode.TryGetValue(code, out var referrer))
                {
                    referrer = new Referrer
                    {
                        Code = code,
                        FirstName = firstName,
                        LastName = lastName,
                        IsActive = true
                    };

                    db.Referrers.Add(referrer);
                    referrersByCode.Add(code, referrer);
                    addedCount++;
                }
                else
                {
                    referrer.FirstName = firstName;
                    referrer.LastName = lastName;
                    referrer.IsActive = true;
                    updatedCount++;
                }
            }

            phase = "ذخیره تغییرات در پایگاه داده";
            logger.LogInformation(
                "Saving Rahkaran employee synchronization changes. Added={AddedCount}, Updated={UpdatedCount}.",
                addedCount,
                updatedCount);

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Rahkaran employee synchronization completed successfully. Added={AddedCount}, Updated={UpdatedCount}, PreviousRecordsPreserved=true.",
                addedCount,
                updatedCount);

            TempData["Success"] =
                $"فهرست کارکنان با موفقیت همگام‌سازی شد؛ {addedCount} کارمند جدید و {updatedCount} کارمند به‌روزرسانی شدند. کارکنان قبلی که در پاسخ جدید نبودند حذف نشدند.";
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Rahkaran employee synchronization failed during phase '{Phase}'.",
                phase);

            TempData["Error"] = "دریافت یا ذخیره فهرست کارکنان از راهکاران ناموفق بود. جزئیات خطا در لاگ برنامه ثبت شده است؛ اطلاعات قبلی حفظ شده‌اند.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CheckSalesRequestReadiness(
        long orderId,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
            return BadRequest(new { ready = false, issues = new[] { "شناسه سفارش معتبر نیست." } });

        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound(new { ready = false, issues = new[] { "سفارش موردنظر پیدا نشد." } });

        var settings = await db.RahkaranIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);

        var issues = new List<string>();
        var checks = new List<string>();

        if (order.Status != OrderStatus.Confirmed)
            issues.Add("وضعیت سفارش باید «تأییدشده» باشد.");
        else
            checks.Add("وضعیت سفارش تأیید شده است.");

        if (order.Customer is null)
            issues.Add("پرونده مشتری داخلی به سفارش متصل نیست.");
        else if (!long.TryParse(order.Customer.RahkaranCustomerId, out var customerId) || customerId <= 0)
            issues.Add("مشتری سفارش به مشتری معتبر راهکاران متصل نشده است.");
        else if (!string.IsNullOrWhiteSpace(order.RahkaranCustomerId) &&
                 !string.Equals(order.RahkaranCustomerId, order.Customer.RahkaranCustomerId, StringComparison.Ordinal))
            issues.Add("شناسه مشتری راهکاران در سفارش با پرونده مشتری داخلی مغایرت دارد.");
        else if (string.IsNullOrWhiteSpace(order.Customer.RahkaranDLCode))
            issues.Add("کد تفصیلی مشتری در اطلاعات همگام‌سازی‌شده راهکاران موجود نیست.");
        else
            checks.Add($"مشتری راهکاران و کد تفصیلی متصل هستند (شناسه مشتری: {customerId}).");

        if (order.Kind != OrderKind.BirthdayCake)
            issues.Add("در حال حاضر فقط سفارش کیک تولد پشتیبانی می‌شود.");
        else
            checks.Add("نوع سفارش در دامنه فعلی یکپارچه‌سازی پشتیبانی می‌شود.");

        if (settings is null)
        {
            issues.Add("تنظیمات یکپارچه‌سازی راهکاران ثبت نشده است.");
        }
        else
        {
            if (settings.SalesOfficeId is not > 0) issues.Add("مرکز فروش تعیین نشده است.");
            if (settings.SalesAreaId is not > 0) issues.Add("حوزه فروش تعیین نشده است.");
            if (settings.SalesTypeId is not > 0) issues.Add("نحوه فروش تعیین نشده است.");
            if (settings.SalesInventoryId is not > 0) issues.Add("شناسه انبار تعیین نشده است.");
            if (settings.SalesPlantId is not > 0) issues.Add("مرکز نگهداری تعیین نشده است.");
            if (settings.SalesCurrencyId is not > 0) issues.Add("ارز تعیین نشده است.");

            var (productId, unitId) = order.CakeType switch
            {
                CakeType.Cream => (settings.CreamCakeProductId, settings.CreamCakeUnitId),
                CakeType.SemiFondant => (settings.SemiFondantCakeProductId, settings.SemiFondantCakeUnitId),
                CakeType.FullFondant => (settings.FullFondantCakeProductId, settings.FullFondantCakeUnitId),
                _ => (null, null)
            };

            if (productId is not > 0) issues.Add("محصول متناظر نوع کیک تعیین نشده است.");
            if (unitId is not > 0) issues.Add("واحد سنجش کیلوگرم برای این محصول تعیین نشده است.");
        }

        if (order.RequestedWeightKg <= 0 || order.PricePerKg < 0 || order.FinalAmount < 0)
            issues.Add("وزن، قیمت هر کیلوگرم یا مبلغ نهایی سفارش معتبر نیست.");
        else
            checks.Add("مقادیر وزن و قیمت سفارش معتبر هستند.");

        if (order.RequestedDepositAmount > 0)
        {
            if (order.DepositAmount < order.RequestedDepositAmount)
            {
                issues.Add("بیعانه درخواستی هنوز به‌طور کامل دریافت نشده است.");
            }
            else
            {
                var depositReceipts = await db.RahkaranReceiptOutbox
                    .AsNoTracking()
                    .Where(x => x.OrderId == order.Id &&
                        ((x.OrderCashPaymentId != null &&
                          db.OrderCashPayments.Any(cp => cp.Id == x.OrderCashPaymentId && cp.Purpose == PaymentPurpose.Deposit)) ||
                         (x.PaymentTransactionId != null &&
                          db.PaymentTransactions.Any(pt => pt.Id == x.PaymentTransactionId && pt.Purpose == PaymentPurpose.Deposit))))
                    .ToListAsync(cancellationToken);

                if (depositReceipts.Count == 0)
                    issues.Add("برای بیعانه دریافتی، رسید معادل در صف ارسال مالی راهکاران وجود ندارد.");
                else if (depositReceipts.Any(x => x.Status != RahkaranReceiptSyncStatus.Succeeded))
                    issues.Add("تمام رسیدهای بیعانه هنوز با موفقیت در راهکاران ثبت نشده‌اند.");
                else
                    checks.Add("بیعانه درخواستی دریافت و تمام رسیدهای آن در راهکاران ثبت شده‌اند.");
            }
        }
        else
        {
            checks.Add("برای این سفارش بیعانه‌ای درخواست نشده است.");
        }

        var existingOutbox = await db.RahkaranSalesRequestOutbox
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Status, x.RemoteSalesRequestId, x.LastError })
            .FirstOrDefaultAsync(cancellationToken);

        if (existingOutbox?.Status == RahkaranSalesRequestSyncStatus.Succeeded)
            issues.Add($"برای این سفارش قبلاً درخواست فروش با شناسه {existingOutbox.RemoteSalesRequestId ?? "نامشخص"} ثبت شده است؛ ارسال مجدد مجاز نیست.");
        else if (existingOutbox?.Status is RahkaranSalesRequestSyncStatus.Processing or RahkaranSalesRequestSyncStatus.NeedsReview)
            issues.Add("وضعیت ارسال قبلی در حال پردازش یا نیازمند بررسی است؛ پیش از هر اقدام باید نتیجه آن مشخص شود.");
        else if (existingOutbox?.Status == RahkaranSalesRequestSyncStatus.Pending)
            checks.Add("یک درخواست ارسال در صف انتظار وجود دارد.");

        return Json(new
        {
            ready = issues.Count == 0,
            orderNumber = order.OrderNumber,
            checks,
            issues,
            existingOutboxStatus = existingOutbox?.Status.ToString(),
            note = "این بررسی فقط خواندنی است و هیچ درخواست فروش یا سند مالی در راهکاران ایجاد نمی‌کند."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        try
        {
            var cashes = await rahkaran.GetCashesAsync(cancellationToken);
            TempData["Success"] = $"ارتباط با راهکاران برقرار است. تعداد صندوق‌های دریافتی: {cashes.Count}";
        }
        catch (Exception)
        {
            TempData["Error"] = "ارتباط با راهکاران برقرار نشد. نشانی سرویس، دسترسی شبکه و مجوز وب‌سرویس را بررسی کنید.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        RahkaranSettingsViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizeIds(model);

        var settings = await db.RahkaranIntegrationSettings
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (settings is null)
        {
            settings = new RahkaranIntegrationSettings
            {
                Id = 1,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.RahkaranIntegrationSettings.Add(settings);
        }

        settings.BranchId = model.BranchId;
        settings.CashId = model.CashId;
        settings.CashAccountingOperationId = model.CashAccountingOperationId;
        settings.CashFlowFactorId = model.CashFlowFactorId;
        settings.PosBankAccountId = model.PosBankAccountId;
        settings.PosAccountingOperationId = model.PosAccountingOperationId;
        settings.PosCashFlowFactorId = model.PosCashFlowFactorId;
        settings.SalesOfficeId = model.SalesOfficeId;
        settings.SalesAreaId = model.SalesAreaId;
        settings.SalesTypeId = model.SalesTypeId;
        settings.SalesInventoryId = model.SalesInventoryId;
        settings.SalesPlantId = model.SalesPlantId;
        settings.SalesCurrencyId = model.SalesCurrencyId;
        settings.SalesPaymentAgreementId = model.SalesPaymentAgreementId;
        settings.CreamCakeProductId = model.CreamCakeProductId;
        settings.CreamCakeUnitId = model.CreamCakeUnitId;
        settings.SemiFondantCakeProductId = model.SemiFondantCakeProductId;
        settings.SemiFondantCakeUnitId = model.SemiFondantCakeUnitId;
        settings.FullFondantCakeProductId = model.FullFondantCakeProductId;
        settings.FullFondantCakeUnitId = model.FullFondantCakeUnitId;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "نگاشت‌های راهکاران در پایگاه داده CakeOrders ذخیره شدند. ارسال درخواست فروش هنوز فعال نشده است.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadMasterDataAsync(
        RahkaranSettingsViewModel model,
        CancellationToken cancellationToken)
    {
        await LoadAsync("شعب", () => rahkaran.GetBranchesAsync(cancellationToken), items =>
            model.Branches = items.Select(x => new SelectListItem(
                $"{x.Title ?? "بدون عنوان"} (شناسه: {x.Id})", x.Id.ToString())).ToList());

        await LoadAsync("صندوق‌ها", () => rahkaran.GetCashesAsync(cancellationToken), items =>
            model.Cashes = items.Select(x => new SelectListItem(
                $"{x.Title ?? "بدون عنوان"} (شناسه: {x.Id})", x.Id.ToString())).ToList());

        await LoadAsync("حساب‌های بانکی", () => rahkaran.GetBankAccountsAsync(cancellationToken), items =>
            model.BankAccounts = items.Select(x => new SelectListItem(
                $"{x.BankName ?? "بانک نامشخص"} - {x.AccountNumber ?? "بدون شماره"} (شناسه: {x.Id})",
                x.Id.ToString())).ToList());

        await LoadAsync("عملیات حسابداری", () => rahkaran.GetAccountingOperationsAsync(cancellationToken), items =>
            model.AccountingOperations = items.Select(x => new SelectListItem(
                $"{x.Code} - {x.Title} (شناسه: {x.Id})", x.Id.ToString())).ToList());

        await LoadAsync("عوامل گردش نقدینگی", () => rahkaran.GetCashFlowFactorsAsync(cancellationToken), items =>
            model.CashFlowFactors = items.Select(x => new SelectListItem(
                $"{x.Code} - {x.Title} (شناسه: {x.Id})", x.Id.ToString())).ToList());

        await LoadAsync("حوزه‌های فروش", () => rahkaran.GetSalesAreasAsync(cancellationToken), items =>
            model.SalesAreas = items
                .Where(x => x.Id > 0)
                .Select(x => new SelectListItem(
                    $"{x.Code} - {x.Name} (شناسه: {x.Id})", x.Id.ToString()))
                .ToList());

        await LoadAsync("مراکز فروش", () => rahkaran.GetSalesOfficesAsync(cancellationToken), items =>
            model.SalesOffices = items
                .Where(x => x.Id > 0)
                .Select(x => new SelectListItem(
                    $"{x.Code} - {x.Name} (شناسه: {x.Id})", x.Id.ToString()))
                .ToList());

        await LoadAsync("نحوه‌های فروش", () => rahkaran.GetSalesTypesAsync(cancellationToken), items =>
            model.SalesTypes = items
                .Where(x => x.Id > 0)
                .Select(x => new SelectListItem(
                    $"{x.Number} - {x.Name} (شناسه: {x.Id})", x.Id.ToString()))
                .ToList());
    }

    private async Task LoadAsync<T>(
        string title,
        Func<Task<IReadOnlyList<T>>> fetch,
        Action<IReadOnlyList<T>> assign)
    {
        try
        {
            var items = await fetch();
            assign(items);
            ViewData["RahkaranSuccessCount"] = (int)(ViewData["RahkaranSuccessCount"] ?? 0) + 1;
            ViewData["RahkaranMessages"] ??= new List<string>();
            ((List<string>)ViewData["RahkaranMessages"]!).Add($"{title}: {items.Count} مورد دریافت شد.");
        }
        catch (Exception)
        {
            ViewData["RahkaranMessages"] ??= new List<string>();
            ((List<string>)ViewData["RahkaranMessages"]!).Add($"{title}: دریافت اطلاعات ناموفق بود.");
        }
    }

    private static void NormalizeIds(RahkaranSettingsViewModel model)
    {
        if (model.BranchId <= 0) model.BranchId = null;
        if (model.CashId <= 0) model.CashId = null;
        if (model.CashAccountingOperationId <= 0) model.CashAccountingOperationId = null;
        if (model.CashFlowFactorId <= 0) model.CashFlowFactorId = null;
        if (model.PosBankAccountId <= 0) model.PosBankAccountId = null;
        if (model.PosAccountingOperationId <= 0) model.PosAccountingOperationId = null;
        if (model.PosCashFlowFactorId <= 0) model.PosCashFlowFactorId = null;
        if (model.SalesOfficeId <= 0) model.SalesOfficeId = null;
        if (model.SalesAreaId <= 0) model.SalesAreaId = null;
        if (model.SalesTypeId <= 0) model.SalesTypeId = null;
        if (model.SalesInventoryId <= 0) model.SalesInventoryId = null;
        if (model.SalesPlantId <= 0) model.SalesPlantId = null;
        if (model.SalesCurrencyId <= 0) model.SalesCurrencyId = null;
        if (model.SalesPaymentAgreementId <= 0) model.SalesPaymentAgreementId = null;
        if (model.CreamCakeProductId <= 0) model.CreamCakeProductId = null;
        if (model.CreamCakeUnitId <= 0) model.CreamCakeUnitId = null;
        if (model.SemiFondantCakeProductId <= 0) model.SemiFondantCakeProductId = null;
        if (model.SemiFondantCakeUnitId <= 0) model.SemiFondantCakeUnitId = null;
        if (model.FullFondantCakeProductId <= 0) model.FullFondantCakeProductId = null;
        if (model.FullFondantCakeUnitId <= 0) model.FullFondantCakeUnitId = null;
    }
}
