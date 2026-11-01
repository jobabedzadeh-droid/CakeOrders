using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using CakeOrders.Web.ViewModels;
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
    IOptions<RahkaranOptions> options) : Controller
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
            ServiceBaseUrl = options.Value.ServiceBaseUrl,
            IntegrationEnabled = options.Value.Enabled
        };

        await LoadMasterDataAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        try
        {
            // GetAllCashes is a documented, read-only endpoint and is used as
            // the connectivity probe. This action never creates a financial document.
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
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "نگاشت‌های انتخاب‌شده در پایگاه داده CakeOrders ذخیره شدند. ثبت مالی همچنان غیرفعال است.";
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
            modelSuccess(title, items.Count);
        }
        catch (Exception)
        {
            // Keep the page usable even if one optional master-data endpoint
            // is unavailable in this Rahkaran installation.
            modelFailure(title);
        }

        void modelSuccess(string name, int count)
        {
            ViewData["RahkaranSuccessCount"] = (int)(ViewData["RahkaranSuccessCount"] ?? 0) + 1;
            ViewData["RahkaranMessages"] ??= new List<string>();
            ((List<string>)ViewData["RahkaranMessages"]!).Add($"{name}: {count} مورد دریافت شد.");
        }

        void modelFailure(string name)
        {
            ViewData["RahkaranMessages"] ??= new List<string>();
            ((List<string>)ViewData["RahkaranMessages"]!).Add($"{name}: دریافت اطلاعات ناموفق بود.");
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
    }
}
