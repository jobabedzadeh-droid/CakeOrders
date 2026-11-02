using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels.ProductManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ProductsController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.WorkshopRoutings)
            .OrderBy(x => x.Category.Name)
            .ThenBy(x => x.Name)
            .Select(x => new ProductListItemViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                CategoryName = x.Category.Name,
                Unit = x.Unit,
                IsActive = x.IsActive,
                RoutingCount = x.WorkshopRoutings.Count
            })
            .ToListAsync(cancellationToken);

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ProductEditViewModel();
        await PopulateFormAsync(model, cancellationToken);
        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductEditViewModel model, CancellationToken cancellationToken)
    {
        await ValidateProductAsync(model, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(model, cancellationToken);
            return View("Edit", model);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var product = new Product
        {
            CategoryId = model.CategoryId,
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Unit = model.Unit,
            Description = Normalize(model.Description),
            IsActive = model.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        await SaveRoutingsAsync(product.Id, model.BranchRoutings, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = "محصول جدید با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.WorkshopRoutings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is null)
            return NotFound();

        var model = new ProductEditViewModel
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            Code = product.Code,
            Name = product.Name,
            Unit = product.Unit,
            Description = product.Description,
            IsActive = product.IsActive,
            BranchRoutings = product.WorkshopRoutings
                .Select(x => new ProductBranchRoutingViewModel
                {
                    BranchId = x.BranchId,
                    WorkshopId = x.WorkshopId
                }).ToList()
        };

        await PopulateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, ProductEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        var product = await db.Products
            .Include(x => x.WorkshopRoutings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is null)
            return NotFound();

        await ValidateProductAsync(model, id, cancellationToken);
        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        product.CategoryId = model.CategoryId;
        product.Code = model.Code.Trim();
        product.Name = model.Name.Trim();
        product.Unit = model.Unit;
        product.Description = Normalize(model.Description);
        product.IsActive = model.IsActive;

        db.ProductWorkshopRoutings.RemoveRange(product.WorkshopRoutings);
        await SaveRoutingsAsync(product.Id, model.BranchRoutings, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = "اطلاعات محصول و کارگاه‌های پیش‌فرض به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(long id, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
            return NotFound();

        product.IsActive = !product.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = product.IsActive ? "محصول فعال شد." : "محصول غیرفعال شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateProductAsync(ProductEditViewModel model, long? excludeId, CancellationToken cancellationToken)
    {
        model.Code = model.Code?.Trim() ?? string.Empty;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!Enum.IsDefined(model.Unit))
            ModelState.AddModelError(nameof(model.Unit), "واحد انتخاب‌شده معتبر نیست.");

        if (!await db.ProductCategories.AnyAsync(x => x.Id == model.CategoryId && x.IsActive, cancellationToken))
            ModelState.AddModelError(nameof(model.CategoryId), "گروه محصول انتخاب‌شده معتبر یا غیرفعال است.");

        if (await db.Products.AnyAsync(x => x.Code == model.Code && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken))
            ModelState.AddModelError(nameof(model.Code), "کد محصول تکراری است.");

        if (await db.Products.AnyAsync(x => x.CategoryId == model.CategoryId && x.Name == model.Name && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken))
            ModelState.AddModelError(nameof(model.Name), "محصولی با این نام در گروه انتخاب‌شده وجود دارد.");

        var activeBranchIds = await db.Branches
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var submittedBranchIds = model.BranchRoutings.Select(x => x.BranchId).ToList();
        if (activeBranchIds.Except(submittedBranchIds).Any() || submittedBranchIds.Distinct().Count() != submittedBranchIds.Count)
            ModelState.AddModelError(string.Empty, "اطلاعات تخصیص کارگاه‌ها معتبر نیست؛ صفحه را تازه‌سازی کنید.");

        foreach (var route in model.BranchRoutings.Where(x => x.WorkshopId.HasValue))
        {
            var validWorkshop = await db.Workshops.AnyAsync(
                x => x.Id == route.WorkshopId.Value &&
                     x.BranchId == route.BranchId &&
                     x.IsActive,
                cancellationToken);

            if (!validWorkshop)
                ModelState.AddModelError(string.Empty, $"کارگاه انتخاب‌شده برای شعبه {route.BranchName} معتبر یا فعال نیست.");
        }
    }

    private async Task SaveRoutingsAsync(long productId, IEnumerable<ProductBranchRoutingViewModel> routes, CancellationToken cancellationToken)
    {
        var validRoutes = routes.Where(x => x.WorkshopId.HasValue);
        foreach (var route in validRoutes)
        {
            db.ProductWorkshopRoutings.Add(new ProductWorkshopRouting
            {
                ProductId = productId,
                BranchId = route.BranchId,
                WorkshopId = route.WorkshopId!.Value,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await Task.CompletedTask;
    }

    private async Task PopulateFormAsync(ProductEditViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = await db.ProductCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CategoryId))
            .ToListAsync(cancellationToken);

        var branches = await db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Workshops)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var existing = model.BranchRoutings.ToDictionary(x => x.BranchId, x => x.WorkshopId);
        model.BranchRoutings = branches.Select(branch =>
        {
            existing.TryGetValue(branch.Id, out var selectedWorkshopId);
            return new ProductBranchRoutingViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                WorkshopId = selectedWorkshopId,
                Workshops = branch.Workshops
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == selectedWorkshopId))
                    .ToList()
            };
        }).ToList();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
