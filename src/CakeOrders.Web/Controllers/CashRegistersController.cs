using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class CashRegistersController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var registers = await db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .OrderBy(x => x.Branch.Name)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return View(registers);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new CashRegisterViewModel();

        await PopulateLists(model, null, cancellationToken);

        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CashRegisterViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizeModel(model);

        await ValidateSelection(model, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateLists(model, null, cancellationToken);
            return View("Edit", model);
        }

        var entity = new CashRegister
        {
            Name = model.Name,
            BranchId = model.BranchId,
            PosTerminalId = model.PosTerminalId,
            IsActive = model.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.CashRegisters.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                nameof(model.PosTerminalId),
                "این دستگاه کارت‌خوان قبلاً به صندوق دیگری اختصاص داده شده است.");

            await PopulateLists(model, null, cancellationToken);
            return View("Edit", model);
        }

        TempData["Success"] = "صندوق جدید با موفقیت ثبت شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await db.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound();

        var model = new CashRegisterViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            BranchId = entity.BranchId,
            PosTerminalId = entity.PosTerminalId,
            IsActive = entity.IsActive
        };

        await PopulateLists(model, entity.PosTerminalId, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        long id,
        CashRegisterViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        NormalizeModel(model);

        var entity = await db.CashRegisters
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound();

        await ValidateSelection(model, id, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateLists(model, entity.PosTerminalId, cancellationToken);
            return View(model);
        }

        entity.Name = model.Name;
        entity.BranchId = model.BranchId;
        entity.PosTerminalId = model.PosTerminalId;
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                nameof(model.PosTerminalId),
                "این دستگاه کارت‌خوان قبلاً به صندوق دیگری اختصاص داده شده است.");

            await PopulateLists(model, entity.PosTerminalId, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "اطلاعات صندوق با موفقیت به‌روزرسانی شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await db.CashRegisters
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound();

        if (!entity.IsActive)
        {
            if (!entity.Branch.IsActive)
            {
                TempData["Error"] = "برای فعال‌سازی صندوق، ابتدا شعبه آن را فعال کنید.";
                return RedirectToAction(nameof(Index));
            }

            if (entity.PosTerminalId.HasValue &&
                (entity.PosTerminal is null || !entity.PosTerminal.IsActive))
            {
                TempData["Error"] = "دستگاه کارت‌خوان اختصاص‌یافته غیرفعال است. ابتدا آن را فعال کنید.";
                return RedirectToAction(nameof(Index));
            }
        }

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = entity.IsActive
            ? "صندوق فعال شد."
            : "صندوق غیرفعال شد.";

        return RedirectToAction(nameof(Index));
    }

    private static void NormalizeModel(CashRegisterViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
    }

    private async Task ValidateSelection(
        CashRegisterViewModel model,
        long? currentId,
        CancellationToken cancellationToken)
    {
        // شناسه کارت‌خوان فعلی صندوق (در حالت ویرایش)
        long? currentTerminalId = null;

        if (currentId.HasValue)
        {
            currentTerminalId = await db.CashRegisters
                .AsNoTracking()
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.PosTerminalId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // بررسی وجود شعبه انتخاب‌شده
        var branch = await db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == model.BranchId,
                cancellationToken);

        if (branch is null)
        {
            ModelState.AddModelError(
                nameof(model.BranchId),
                "شعبه انتخاب‌شده معتبر نیست.");
        }
        else if (!branch.IsActive && model.IsActive)
        {
            ModelState.AddModelError(
                nameof(model.BranchId),
                "امکان ثبت یا فعال‌سازی صندوق در شعبه غیرفعال وجود ندارد.");
        }

        // اگر کارت‌خوان انتخاب نشده باشد، ادامه بررسی لازم نیست
        if (!model.PosTerminalId.HasValue)
            return;

        // بررسی وجود کارت‌خوان
        var terminal = await db.PosTerminals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == model.PosTerminalId.Value,
                cancellationToken);

        if (terminal is null)
        {
            ModelState.AddModelError(
                nameof(model.PosTerminalId),
                "دستگاه کارت‌خوان انتخاب‌شده معتبر نیست.");

            return;
        }

        // کارت‌خوان غیرفعال فقط برای حفظ اتصال قبلی همین صندوق مجاز است
        var isCurrentTerminal = currentId.HasValue &&
                                currentTerminalId == model.PosTerminalId;

        if (!terminal.IsActive && !isCurrentTerminal)
        {
            ModelState.AddModelError(
                nameof(model.PosTerminalId),
                "امکان اختصاص دستگاه کارت‌خوان غیرفعال وجود ندارد.");
        }

        // جلوگیری از اختصاص کارت‌خوان به صندوق دیگر
        var isAssignedToAnotherRegister = await db.CashRegisters
            .AsNoTracking()
            .AnyAsync(
                x => x.PosTerminalId == model.PosTerminalId.Value &&
                     (!currentId.HasValue || x.Id != currentId.Value),
                cancellationToken);

        if (isAssignedToAnotherRegister)
        {
            ModelState.AddModelError(
                nameof(model.PosTerminalId),
                "این دستگاه کارت‌خوان قبلاً به صندوق دیگری اختصاص داده شده است.");
        }
    }

    private async Task PopulateLists(
        CashRegisterViewModel model,
        long? currentTerminalId,
        CancellationToken cancellationToken)
    {
        var branches = await db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive || x.Id == model.BranchId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        model.Branches = branches.Select(x => new SelectListItem
        {
            Value = x.Id.ToString(),
            Text = x.Name + (x.IsActive ? "" : " (غیرفعال)"),
            Selected = x.Id == model.BranchId
        }).ToList();

        var terminals = await db.PosTerminals
            .AsNoTracking()
            .Where(x =>
                x.IsActive ||
                (currentTerminalId.HasValue && x.Id == currentTerminalId.Value))
            .Where(x =>
                !db.CashRegisters.Any(r =>
                    r.PosTerminalId == x.Id &&
                    (!currentTerminalId.HasValue || r.PosTerminalId != currentTerminalId.Value)))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        model.PosTerminals = terminals.Select(x => new SelectListItem
        {
            Value = x.Id.ToString(),
            Text = $"{x.Name} - {x.IpAddress}:{x.Port}" +
                   (x.IsActive ? "" : " (غیرفعال)"),
            Selected = x.Id == model.PosTerminalId
        }).ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateBridgeCredentials(
    long id,
    CancellationToken cancellationToken)
    {
        var register = await db.CashRegisters
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (register is null)
            return NotFound();

        if (!register.IsActive)
        {
            TempData["Error"] = "برای صندوق غیرفعال امکان ایجاد دسترسی وجود ندارد.";
            return RedirectToAction(nameof(Index));
        }

        if (!register.PosTerminalId.HasValue)
        {
            TempData["Error"] = "ابتدا یک دستگاه کارت‌خوان به صندوق اختصاص دهید.";
            return RedirectToAction(nameof(Index));
        }

        var installationId = Guid.NewGuid().ToString("N");

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var secretHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

        register.BridgeInstallationId = installationId;
        register.BridgeSecretHash = secretHash;
        register.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            success = true,
            cashRegisterId = register.Id,
            installationId,
            secret,
            message = "این رمز فقط یک بار نمایش داده می‌شود. آن را در محل امن نگهداری کنید."
        });
    }
}