using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Identity;
using CakeOrders.Web.ViewModels.WorkshopManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class WorkshopsController(
    CakeOrdersDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workshops = await db.Workshops
            .AsNoTracking()
            .Include(x => x.Branch)
            .OrderBy(x => x.Branch.Name)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var members = await db.Users
            .AsNoTracking()
            .Where(x => x.WorkshopId != null)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var membersByWorkshop = members
            .Where(x => x.WorkshopId.HasValue && x.WorkshopRole.HasValue)
            .GroupBy(x => x.WorkshopId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<WorkshopMemberItemViewModel>)group
                    .Select(user => new WorkshopMemberItemViewModel
                    {
                        UserId = user.Id,
                        DisplayName = user.DisplayName,
                        UserName = user.UserName ?? string.Empty,
                        PhoneNumber = user.PhoneNumber,
                        Role = user.WorkshopRole!.Value,
                        IsActive = user.IsActive
                    })
                    .OrderBy(x => x.Role)
                    .ThenBy(x => x.DisplayName)
                    .ToList());

        var model = new WorkshopManagementIndexViewModel
        {
            Workshops = workshops.Select(workshop =>
                new WorkshopManagementItemViewModel
                {
                    Id = workshop.Id,
                    Name = workshop.Name,
                    BranchName = workshop.Branch.Name,
                    IsActive = workshop.IsActive,
                    Members = membersByWorkshop.GetValueOrDefault(
                        workshop.Id,
                        Array.Empty<WorkshopMemberItemViewModel>())
                }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
    long workshopId,
    bool isActive,
    CancellationToken cancellationToken)
    {
        var workshop = await db.Workshops
            .FirstOrDefaultAsync(
                x => x.Id == workshopId,
                cancellationToken);

        if (workshop is null)
            return NotFound();

        if (workshop.IsActive == isActive)
        {
            TempData["ErrorMessage"] = "وضعیت کارگاه تغییری نکرده است.";
            return RedirectToAction(nameof(Index));
        }

        workshop.IsActive = isActive;

        await db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = isActive
            ? $"کارگاه «{workshop.Name}» با موفقیت فعال شد."
            : $"کارگاه «{workshop.Name}» غیرفعال شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Assign(
        long workshopId,
        CancellationToken cancellationToken)
    {
        var workshop = await db.Workshops
            .AsNoTracking()
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == workshopId, cancellationToken);

        if (workshop is null)
            return NotFound();

        if (!workshop.IsActive)
        {
            TempData["ErrorMessage"] = "امکان تخصیص عضو به کارگاه غیرفعال وجود ندارد.";
            return RedirectToAction(nameof(Index));
        }

        var model = new AssignWorkshopMemberViewModel
        {
            WorkshopId = workshop.Id,
            WorkshopName = workshop.Name,
            BranchName = workshop.Branch.Name,
            Role = WorkshopMemberRole.Employee
        };

        await PopulateAvailableUsersAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        AssignWorkshopMemberViewModel model,
        CancellationToken cancellationToken)
    {
        var workshop = await db.Workshops
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == model.WorkshopId, cancellationToken);

        if (workshop is null)
            return NotFound();

        model.WorkshopName = workshop.Name;
        model.BranchName = workshop.Branch.Name;

        if (!workshop.IsActive)
            ModelState.AddModelError(string.Empty, "کارگاه غیرفعال است و عضو جدید نمی‌پذیرد.");

        if (!Enum.IsDefined(model.Role))
            ModelState.AddModelError(nameof(model.Role), "نقش انتخاب‌شده معتبر نیست.");

        ApplicationUser? user = null;

        if (!string.IsNullOrWhiteSpace(model.UserId))
            user = await userManager.FindByIdAsync(model.UserId);

        if (user is null)
        {
            if (!ModelState.ContainsKey(nameof(model.UserId)))
                ModelState.AddModelError(nameof(model.UserId), "کاربر انتخاب‌شده پیدا نشد.");
        }
        else
        {
            if (!user.IsActive)
                ModelState.AddModelError(nameof(model.UserId), "امکان تخصیص کاربر غیرفعال وجود ندارد.");

            if (await userManager.IsInRoleAsync(user, "Admin"))
                ModelState.AddModelError(nameof(model.UserId), "حساب مدیر سیستم را نمی‌توان به‌عنوان عضو کارگاه تخصیص داد.");

            if (user.WorkshopId.HasValue && user.WorkshopId.Value != workshop.Id)
                ModelState.AddModelError(nameof(model.UserId), "این کاربر در کارگاه دیگری عضو است؛ ابتدا عضویت قبلی او را حذف کنید.");
        }

        if (user is not null &&
            model.Role == WorkshopMemberRole.Manager &&
            await db.Users.AnyAsync(
                x => x.WorkshopId == workshop.Id &&
                     x.WorkshopRole == WorkshopMemberRole.Manager &&
                     x.Id != user.Id,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Role), "این کارگاه از قبل مسئول دارد. ابتدا مسئول فعلی را تغییر وضعیت دهید یا عضویتش را حذف کنید.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAvailableUsersAsync(model, cancellationToken);
            return View(model);
        }

        user!.WorkshopId = workshop.Id;
        user.WorkshopRole = model.Role;
        user.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await PopulateAvailableUsersAsync(model, cancellationToken);
                return View(model);
            }
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "ذخیره عضویت انجام نشد؛ احتمالاً مسئول دیگری هم‌زمان برای این کارگاه تعیین شده است.");
            await PopulateAvailableUsersAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"«{user.DisplayName}» با موفقیت به کارگاه «{workshop.Name}» تخصیص یافت.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
            return NotFound();

        if (!user.WorkshopId.HasValue)
        {
            TempData["ErrorMessage"] = "این کاربر در هیچ کارگاهی عضو نیست.";
            return RedirectToAction(nameof(Index));
        }

        user.WorkshopId = null;
        user.WorkshopRole = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("، ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"عضویت «{user.DisplayName}» از کارگاه حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAvailableUsersAsync(
        AssignWorkshopMemberViewModel model,
        CancellationToken cancellationToken)
    {
        var adminIds = (await userManager.GetUsersInRoleAsync("Admin"))
            .Select(x => x.Id)
            .ToHashSet();

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        (x.WorkshopId == null || x.WorkshopId == model.WorkshopId))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        model.AvailableUsers = users
            .Where(x => !adminIds.Contains(x.Id))
            .Select(x => new SelectListItem(
                $"{x.DisplayName} ({x.UserName})",
                x.Id,
                x.Id == model.UserId))
            .ToList();
    }
}
