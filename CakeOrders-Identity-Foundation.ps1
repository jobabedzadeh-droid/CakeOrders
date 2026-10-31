$ErrorActionPreference = 'Stop'

# Run this script from the CakeOrders-starter repository root.
$required = @(
    'src/CakeOrders.Web/Program.cs',
    'src/CakeOrders.Web/Controllers/AdminAuthController.cs',
    'src/CakeOrders.Infrastructure/Data/CakeOrdersDbContext.cs',
    'src/CakeOrders.Infrastructure/CakeOrders.Infrastructure.csproj',
    'src/CakeOrders.Domain/Entities/Order.cs',
    'src/CakeOrders.Domain/Entities/OrderStatusHistory.cs'
)
foreach ($f in $required) {
    if (-not (Test-Path $f)) { throw "Missing expected file: $f. Run from the repository root." }
}

# Fail closed if the script was already run.
$ctxPath = 'src/CakeOrders.Infrastructure/Data/CakeOrdersDbContext.cs'
$ctx = Get-Content $ctxPath -Raw
if ($ctx.Contains('IdentityDbContext<ApplicationUser>')) {
    throw 'IdentityDbContext already exists; no changes made.'
}

# Back up all files this script may edit.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = ".cakeorders-identity-backup-$stamp"
$editFiles = @(
    'src/CakeOrders.Infrastructure/CakeOrders.Infrastructure.csproj',
    'src/CakeOrders.Infrastructure/Data/CakeOrdersDbContext.cs',
    'src/CakeOrders.Web/Program.cs',
    'src/CakeOrders.Web/Controllers/AdminAuthController.cs',
    'src/CakeOrders.Domain/Entities/Order.cs',
    'src/CakeOrders.Domain/Entities/OrderStatusHistory.cs',
    'src/CakeOrders.Web/Controllers/HomeController.cs'
)
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($f in $editFiles) {
    if (Test-Path $f) {
        $dest = Join-Path $backup $f
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        Copy-Item $f $dest
    }
}
Write-Host "Backup created: $backup"

# Add Identity EF package, aligned with existing EF Core 10.0.14.
$csprojPath = 'src/CakeOrders.Infrastructure/CakeOrders.Infrastructure.csproj'
$csproj = Get-Content $csprojPath -Raw
$sqlRef = '<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.14" />'
if (-not $csproj.Contains($sqlRef)) { throw 'Expected EF Core SQL Server package reference was not found.' }
$csproj = $csproj.Replace($sqlRef, '<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.14" />' + "`r`n    " + $sqlRef)
Set-Content $csprojPath -Value $csproj -Encoding utf8

New-Item -ItemType Directory -Force -Path 'src/CakeOrders.Infrastructure/Identity' | Out-Null
@'
using Microsoft.AspNetCore.Identity;

namespace CakeOrders.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
'@ | Set-Content 'src/CakeOrders.Infrastructure/Identity/ApplicationUser.cs' -Encoding utf8

@'
namespace CakeOrders.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}
'@ | Set-Content 'src/CakeOrders.Domain/Entities/AuditLog.cs' -Encoding utf8

# Convert the existing context without discarding existing entity mappings.
$ctx = Get-Content $ctxPath -Raw
$ctx = $ctx.Replace(
    'using CakeOrders.Domain.Entities;',
    'using CakeOrders.Domain.Entities;' + "`r`n" +
    'using CakeOrders.Infrastructure.Identity;' + "`r`n" +
    'using Microsoft.AspNetCore.Identity.EntityFrameworkCore;'
)
$ctx = $ctx.Replace(' : DbContext(options)', ' : IdentityDbContext<ApplicationUser>(options)')
$ctx = $ctx.Replace(
    '    public DbSet<Order> Orders => Set<Order>();',
    '    public DbSet<Order> Orders => Set<Order>();' + "`r`n" +
    '    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();'
)
$ctx = $ctx.Replace(
    '            entity.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();',
    '            entity.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();' + "`r`n" +
    '            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);' + "`r`n" +
    '            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);'
)
$ctx = $ctx.Replace(
    '            entity.Property(x => x.Note).HasMaxLength(1000);',
    '            entity.Property(x => x.Note).HasMaxLength(1000);' + "`r`n" +
    '            entity.Property(x => x.ChangedByUserId).HasMaxLength(450);'
)
$closing = "        });`r`n    }`r`n}"
if (-not $ctx.Contains($closing)) { throw 'Could not locate end of OnModelCreating; inspect backup, no more edits performed.' }
$auditConfig = @'
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.DetailsJson).HasMaxLength(4000);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
        });
'@
$ctx = $ctx.Replace($closing, "        });`r`n" + $auditConfig + "    }`r`n}")
Set-Content $ctxPath -Value $ctx -Encoding utf8

# Nullable user identifiers preserve compatibility with existing orders/history rows.
$orderPath = 'src/CakeOrders.Domain/Entities/Order.cs'
$order = Get-Content $orderPath -Raw
if (-not $order.Contains('public string? CreatedByUserId')) {
    $order = $order.Replace(
        '    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;',
        '    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;' + "`r`n" +
        '    public string? CreatedByUserId { get; set; }' + "`r`n" +
        '    public string? UpdatedByUserId { get; set; }'
    )
}
Set-Content $orderPath -Value $order -Encoding utf8

$historyPath = 'src/CakeOrders.Domain/Entities/OrderStatusHistory.cs'
$history = Get-Content $historyPath -Raw
if (-not $history.Contains('ChangedByUserId')) { throw 'Expected ChangedByUserId property missing; stopped.' }
Set-Content $historyPath -Value $history -Encoding utf8

# Replace hard-coded configuration-based login with Identity sign-in.
@'
using CakeOrders.Infrastructure.Identity;
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
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست.");
            return View();
        }

        var result = await signInManager.PasswordSignInAsync(
            user, password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست.");
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
}
'@ | Set-Content 'src/CakeOrders.Web/Controllers/AdminAuthController.cs' -Encoding utf8

# Set Identity, a secure cookie, and a fallback authorization policy.
@'
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<CakeOrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CakeOrders")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<CakeOrdersDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CakeOrders.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/AdminAuth/Login";
    options.AccessDeniedPath = "/AdminAuth/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
'@ | Set-Content 'src/CakeOrders.Web/Program.cs' -Encoding utf8

Write-Host ''
Write-Host 'Identity foundation code updated.'
Write-Host 'No migrations were created or applied. No admin account was created.'
Write-Host "Review backups in: $backup"
Write-Host 'Next run: dotnet restore; dotnet build. Do not run database update yet.'
