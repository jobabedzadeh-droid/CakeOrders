using CakeOrders.Infrastructure.Identity;
using CakeOrders.Web.ViewModels.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UserManagementController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var result = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);

            result.Add(new UserListItemViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = string.Join("، ", roles),
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc
            });
        }

        return View(result);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (model.Role != "Admin" && model.Role != "User")
        {
            ModelState.AddModelError(nameof(model.Role), "نقش انتخاب‌شده معتبر نیست.");
            return View(model);
        }

        if (!await roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "نقش انتخاب‌شده در سامانه تعریف نشده است.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            DisplayName = model.DisplayName.Trim(),
            UserName = model.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email)
                ? null
                : model.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, model.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, model.Role);

        if (!roleResult.Succeeded)
        {
            // اگر تخصیص نقش شکست خورد، حساب ناقص باقی نماند.
            await userManager.DeleteAsync(user);

            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        TempData["SuccessMessage"] = "کاربر جدید با موفقیت ایجاد شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        // مدیر اجازه غیرفعال‌کردن حساب خودش را ندارد.
        var currentUserId = userManager.GetUserId(User);

        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "امکان غیرفعال‌کردن حساب کاربری خودتان وجود ندارد.";
            return RedirectToAction(nameof(Index));
        }

        // اگر کاربر مدیر است، نباید آخرین مدیر فعال سامانه غیرفعال شود.
        if (user.IsActive)
        {
            var isAdmin = await userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                var activeAdmins = (await userManager.GetUsersInRoleAsync("Admin"))
                    .Count(x => x.IsActive);

                if (activeAdmins <= 1)
                {
                    TempData["ErrorMessage"] = "امکان غیرفعال‌کردن آخرین مدیر فعال سامانه وجود ندارد.";
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(
                "، ",
                result.Errors.Select(x => x.Description));

            return RedirectToAction(nameof(Index));
        }

        // تغییر SecurityStamp نشست‌های قبلی را برای اعتبارسنجی مجدد علامت‌گذاری می‌کند.
        await userManager.UpdateSecurityStampAsync(user);

        TempData["SuccessMessage"] = user.IsActive
            ? "حساب کاربر با موفقیت فعال شد."
            : "حساب کاربر با موفقیت غیرفعال شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        var model = new ResetUserPasswordViewModel
        {
            UserId = user.Id,
            DisplayName = user.DisplayName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByIdAsync(model.UserId);

        if (user is null)
            return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var result = await userManager.ResetPasswordAsync(
            user,
            token,
            model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            model.DisplayName = user.DisplayName;
            return View(model);
        }

        await userManager.UpdateSecurityStampAsync(user);

        TempData["SuccessMessage"] =
            $"رمز عبور حساب «{user.DisplayName}» با موفقیت بازنشانی شد.";

        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        var roles = await userManager.GetRolesAsync(user);

        var model = new EditUserViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = roles.FirstOrDefault() ?? "User",
            IsActive = user.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (model.Role != "Admin" && model.Role != "User")
        {
            ModelState.AddModelError(nameof(model.Role), "نقش انتخاب‌شده معتبر نیست.");
            return View(model);
        }

        if (!await roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "نقش انتخاب‌شده در سامانه تعریف نشده است.");
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.Id);

        if (user is null)
            return NotFound();

        var currentUserId = userManager.GetUserId(User);

        // مدیر نمی‌تواند حساب خودش را غیرفعال کند.
        if (user.Id == currentUserId && !model.IsActive)
        {
            ModelState.AddModelError(nameof(model.IsActive),
                "امکان غیرفعال‌کردن حساب خودتان وجود ندارد.");
            return View(model);
        }

        // جلوگیری از غیرفعال‌کردن یا تنزل نقش آخرین مدیر فعال
        var wasAdmin = await userManager.IsInRoleAsync(user, "Admin");

        if (wasAdmin && user.IsActive &&
            (!model.IsActive || model.Role != "Admin"))
        {
            var activeAdmins = (await userManager.GetUsersInRoleAsync("Admin"))
                .Count(x => x.IsActive);

            if (activeAdmins <= 1)
            {
                ModelState.AddModelError(string.Empty,
                    "تغییر نقش یا غیرفعال‌کردن آخرین مدیر فعال مجاز نیست.");
                return View(model);
            }
        }

        user.DisplayName = model.DisplayName.Trim();
        user.UserName = model.UserName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email)
            ? null
            : model.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null
            : model.PhoneNumber.Trim();

        user.IsActive = model.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        var currentRoles = await userManager.GetRolesAsync(user);

        if (!currentRoles.Contains(model.Role))
        {
            if (currentRoles.Count > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "اطلاعات کاربر ذخیره شد، اما تغییر نقش انجام نشد.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var addResult = await userManager.AddToRoleAsync(user, model.Role);

            if (!addResult.Succeeded)
            {
                TempData["ErrorMessage"] = "اطلاعات کاربر ذخیره شد، اما تخصیص نقش جدید انجام نشد.";
                return RedirectToAction(nameof(Index));
            }
        }

        await userManager.UpdateSecurityStampAsync(user);

        TempData["SuccessMessage"] = "اطلاعات کاربر با موفقیت ویرایش شد.";

        return RedirectToAction(nameof(Index));
    }
}