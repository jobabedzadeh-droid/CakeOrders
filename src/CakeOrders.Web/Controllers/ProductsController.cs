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
    public async Task<IActionResult> Index(
    string? search,
    long? categoryId,
    bool? isActive,
    int page = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        // محدود کردن تعداد رکوردهای هر صفحه
        pageSize = pageSize switch
        {
            10 or 20 or 50 or 100 => pageSize,
            _ => 20
        };

        page = Math.Max(1, page);

        var query = db.Products.AsNoTracking();

        // جست‌وجو بر اساس نام یا کد محصول
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.Code.Contains(search));
        }

        // فیلتر گروه محصول
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        // فیلتر وضعیت
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        // تعداد کل محصولات پس از اعمال فیلترها
        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = Math.Max(
            1,
            (int)Math.Ceiling(totalCount / (double)pageSize));

        if (page > totalPages)
            page = totalPages;

        // دریافت محصولات صفحه جاری
        var products = await query
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
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // اطلاعات گروه‌ها برای فیلتر
        var categories = await db.ProductCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Text = x.Name,
                Value = x.Id.ToString()
            })
            .ToListAsync(cancellationToken);

        categories.Insert(0, new SelectListItem
        {
            Text = "همه گروه‌ها",
            Value = "",
            Selected = !categoryId.HasValue || categoryId.Value <= 0
        });

        foreach (var category in categories.Where(x => !string.IsNullOrEmpty(x.Value)))
        {
            category.Selected = category.Value == categoryId?.ToString();
        }

        // اطلاعات صفحه برای View
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.IsActive = isActive;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.Categories = categories;

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

        SaveRoutings(product.Id, model.BranchRoutings);
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

        SaveRoutings(product.Id, model.BranchRoutings, product.WorkshopRoutings);
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

    private async Task ValidateProductAsync(
    ProductEditViewModel model,
    long? excludeId,
    CancellationToken cancellationToken)
    {
        model.Code = model.Code?.Trim() ?? string.Empty;
        model.Name = model.Name?.Trim() ?? string.Empty;

        model.BranchRoutings ??= [];
        var routes = model.BranchRoutings;

        if (!Enum.IsDefined(model.Unit))
        {
            ModelState.AddModelError(
                nameof(model.Unit),
                "واحد انتخاب‌شده معتبر نیست.");
        }

        var validCategory = await db.ProductCategories.AnyAsync(
            x => x.Id == model.CategoryId &&
                 (x.IsActive ||
                  (excludeId.HasValue &&
                   db.Products.Any(p =>
                       p.Id == excludeId.Value &&
                       p.CategoryId == x.Id))),
            cancellationToken);

        if (!validCategory)
        {
            ModelState.AddModelError(
                nameof(model.CategoryId),
                "گروه محصول انتخاب‌شده معتبر یا غیرفعال است.");
        }

        var duplicateCode = await db.Products.AnyAsync(
            x => x.Code == model.Code &&
                 (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

        if (duplicateCode)
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "کد محصول تکراری است.");
        }

        var duplicateName = await db.Products.AnyAsync(
            x => x.CategoryId == model.CategoryId &&
                 x.Name == model.Name &&
                 (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

        if (duplicateName)
        {
            ModelState.AddModelError(
                nameof(model.Name),
                "محصولی با این نام در گروه انتخاب‌شده وجود دارد.");
        }

        // اعتبارسنجی شعبه‌ها
        var activeBranchIds = await db.Branches
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var submittedBranchIds = routes
            .Select(x => x.BranchId)
            .ToList();

        if (activeBranchIds.Except(submittedBranchIds).Any() ||
            submittedBranchIds.Except(activeBranchIds).Any() ||
            submittedBranchIds.Distinct().Count() != submittedBranchIds.Count)
        {
            ModelState.AddModelError(
                string.Empty,
                "اطلاعات تخصیص کارگاه‌ها معتبر نیست؛ صفحه را تازه‌سازی کنید.");
        }

        // اعتبارسنجی کارگاه‌های انتخاب‌شده
        var selectedRoutes = routes
            .Where(x => x.WorkshopId.HasValue)
            .Select(x => new
            {
                x.BranchId,
                WorkshopId = x.WorkshopId!.Value
            })
            .ToList();

        var selectedWorkshopIds = selectedRoutes
            .Select(x => x.WorkshopId)
            .Distinct()
            .ToList();

        var validWorkshopPairs = await db.Workshops
            .Where(x => selectedWorkshopIds.Contains(x.Id) && x.IsActive)
            .Select(x => new
            {
                x.BranchId,
                WorkshopId = x.Id
            })
            .ToListAsync(cancellationToken);

        var validWorkshopSet = validWorkshopPairs
            .Select(x => (x.BranchId, x.WorkshopId))
            .ToHashSet();

        foreach (var route in selectedRoutes)
        {
            if (!validWorkshopSet.Contains(
                    (route.BranchId, route.WorkshopId)))
            {
                var branchName = routes
                    .First(x => x.BranchId == route.BranchId)
                    .BranchName;

                ModelState.AddModelError(
                    string.Empty,
                    $"کارگاه انتخاب‌شده برای شعبه {branchName} معتبر یا فعال نیست.");
            }
        }
    }

    private void SaveRoutings(
    long productId,
    IEnumerable<ProductBranchRoutingViewModel> routes,
    ICollection<ProductWorkshopRouting>? existingRoutings = null)
    {
        var submittedRoutes = routes.ToDictionary(x => x.BranchId);

        var existingByBranch = (existingRoutings ?? [])
            .ToDictionary(x => x.BranchId);

        // حذف تخصیص‌هایی که از فرم حذف شده‌اند
        // یا دیگر برای آن شعبه کارگاهی انتخاب نشده است.
        foreach (var existing in existingByBranch.Values.ToList())
        {
            if (!submittedRoutes.TryGetValue(existing.BranchId, out var submitted) ||
                !submitted.WorkshopId.HasValue)
            {
                db.ProductWorkshopRoutings.Remove(existing);
            }
        }

        // افزودن تخصیص‌های جدید یا به‌روزرسانی تخصیص‌های قبلی
        foreach (var route in routes)
        {
            if (route.WorkshopId is not long workshopId)
                continue;

            if (existingByBranch.TryGetValue(route.BranchId, out var existing))
            {
                if (existing.WorkshopId != workshopId)
                {
                    existing.WorkshopId = workshopId;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
            else
            {
                db.ProductWorkshopRoutings.Add(new ProductWorkshopRouting
                {
                    ProductId = productId,
                    BranchId = route.BranchId,
                    WorkshopId = workshopId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }
    }

    private async Task PopulateFormAsync(
    ProductEditViewModel model,
    CancellationToken cancellationToken)
    {
        model.Categories = await db.ProductCategories
            .AsNoTracking()
            .Where(x => x.IsActive || x.Id == model.CategoryId)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(
                x.Name,
                x.Id.ToString(),
                x.Id == model.CategoryId))
            .ToListAsync(cancellationToken);

        var branches = await db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Workshops)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var existing = (model.BranchRoutings ?? [])
            .GroupBy(x => x.BranchId)
            .ToDictionary(
                group => group.Key,
                group => group.First().WorkshopId);

        model.BranchRoutings = branches.Select(branch =>
        {
            existing.TryGetValue(branch.Id, out var selectedWorkshopId);

            return new ProductBranchRoutingViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                WorkshopId = selectedWorkshopId,

                Workshops = branch.Workshops
                    .Where(x => x.IsActive || x.Id == selectedWorkshopId)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(
                        x.IsActive ? x.Name : $"{x.Name} (غیرفعال)",
                        x.Id.ToString(),
                        x.Id == selectedWorkshopId))
                    .ToList()
            };
        }).ToList();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
