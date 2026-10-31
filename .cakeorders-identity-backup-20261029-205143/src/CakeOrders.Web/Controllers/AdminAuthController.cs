using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CakeOrders.Web.Controllers;

public sealed class AdminAuthController(IConfiguration configuration) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        var expectedUsername = configuration["CakeOrdersAdmin:Username"];
        var expectedPassword = configuration["CakeOrdersAdmin:Password"];

        if (string.IsNullOrWhiteSpace(expectedUsername) ||
            string.IsNullOrWhiteSpace(expectedPassword))
        {
            ModelState.AddModelError(
                string.Empty,
                "ورود مدیر پیکربندی نشده است. متغیرهای محیطی CakeOrdersAdmin__Username و CakeOrdersAdmin__Password را روی سرور تنظیم کنید.");

            return View();
        }

        if (!FixedTimeEquals(username ?? string.Empty, expectedUsername) ||
            !FixedTimeEquals(password ?? string.Empty, expectedPassword))
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست.");
            return View();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, expectedUsername),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "PosTerminals");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);

        return a.Length == b.Length &&
               CryptographicOperations.FixedTimeEquals(a, b);
    }
}