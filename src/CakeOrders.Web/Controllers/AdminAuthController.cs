using CakeOrders.Infrastructure.Identity;
using CakeOrders.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CakeOrders.Web.Controllers;

public sealed class AdminAuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "نام کاربری و رمز عبور را وارد کنید.");
            return View();
        }

        var user = await userManager.FindByNameAsync(username.Trim());
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست");
            return View();
        }

        var result = await signInManager.PasswordSignInAsync(
            user, password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست");
            return View();
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        var roles = await userManager.GetRolesAsync(user);

        var model = new ProfileViewModel
        {
            DisplayName = user.DisplayName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = string.Join("، ", roles)
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        if (!ModelState.IsValid)
            return View(model);

        user.DisplayName = model.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email)
            ? null
            : model.Email.Trim();

        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null
            : model.PhoneNumber.Trim();

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        TempData["SuccessMessage"] = "اطلاعات پروفایل با موفقیت ذخیره شد.";

        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);

        TempData["SuccessMessage"] = "رمز عبور با موفقیت تغییر کرد.";

        return RedirectToAction(nameof(Profile));
    }
}
