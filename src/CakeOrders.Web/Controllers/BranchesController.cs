using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class BranchesController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var branches = await db.Branches
            .AsNoTracking()
            .Include(x => x.CashRegisters)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return View(branches);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Edit", new BranchViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        BranchViewModel model,
        CancellationToken cancellationToken)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Address = string.IsNullOrWhiteSpace(model.Address)
            ? null
            : model.Address.Trim();

        await ValidateBranchName(model.Name, null, cancellationToken);

        if (!ModelState.IsValid)
            return View("Edit", model);

        var branch = new Branch
        {
            Name = model.Name,
            Address = model.Address,
            IsActive = model.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "شعبه جدید با موفقیت ثبت شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        long id,
        CancellationToken cancellationToken)
    {
        var branch = await db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (branch is null)
            return NotFound();

        return View(new BranchViewModel
        {
            Id = branch.Id,
            Name = branch.Name,
            Address = branch.Address,
            IsActive = branch.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        long id,
        BranchViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Address = string.IsNullOrWhiteSpace(model.Address)
            ? null
            : model.Address.Trim();

        await ValidateBranchName(model.Name, id, cancellationToken);

        if (!ModelState.IsValid)
            return View(model);

        var branch = await db.Branches
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (branch is null)
            return NotFound();

        branch.Name = model.Name;
        branch.Address = model.Address;
        branch.IsActive = model.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "اطلاعات شعبه به‌روزرسانی شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(
        long id,
        CancellationToken cancellationToken)
    {
        var branch = await db.Branches
            .Include(x => x.CashRegisters)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (branch is null)
            return NotFound();

        if (branch.IsActive && branch.CashRegisters.Any(x => x.IsActive))
        {
            TempData["Error"] =
                "ابتدا صندوق‌های فعال این شعبه را غیرفعال کنید.";

            return RedirectToAction(nameof(Index));
        }

        branch.IsActive = !branch.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = branch.IsActive
            ? "شعبه فعال شد."
            : "شعبه غیرفعال شد.";

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateBranchName(
        string name,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var exists = await db.Branches.AnyAsync(
            x => x.Name == name &&
                 (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

        if (exists)
        {
            ModelState.AddModelError(
                nameof(BranchViewModel.Name),
                "شعبه‌ای با این نام قبلاً ثبت شده است.");
        }
    }
}