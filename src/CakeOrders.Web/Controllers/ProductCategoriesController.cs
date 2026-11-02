using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels.ProductManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ProductCategoriesController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await db.ProductCategories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductCategoryEditViewModel { Id = x.Id, Name = x.Name, IsActive = x.IsActive })
            .ToListAsync(cancellationToken);
        ViewBag.ProductCounts = await db.Products.AsNoTracking()
            .GroupBy(x => x.CategoryId)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new ProductCategoryEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCategoryEditViewModel model, CancellationToken cancellationToken)
    {
        await ValidateName(model.Name, null, cancellationToken);
        if (!ModelState.IsValid) return View("Edit", model);
        db.ProductCategories.Add(new ProductCategory { Name = model.Name.Trim(), IsActive = model.IsActive });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "گروه محصول ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var item = await db.ProductCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();
        return View(new ProductCategoryEditViewModel { Id = item.Id, Name = item.Name, IsActive = item.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, ProductCategoryEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();
        await ValidateName(model.Name, id, cancellationToken);
        if (!ModelState.IsValid) return View(model);
        var item = await db.ProductCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();
        item.Name = model.Name.Trim();
        item.IsActive = model.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "گروه محصول به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateName(string? name, long? exceptId, CancellationToken cancellationToken)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (await db.ProductCategories.AnyAsync(x => x.Name == name && (!exceptId.HasValue || x.Id != exceptId.Value), cancellationToken))
            ModelState.AddModelError(nameof(ProductCategoryEditViewModel.Name), "گروهی با این نام قبلاً ثبت شده است.");
    }
}