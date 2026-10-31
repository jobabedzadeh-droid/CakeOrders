using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Identity;
using CakeOrders.Web.Identity;
using CakeOrders.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
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

// SadadBridge uses a separate header-based scheme. The Identity cookie remains
// the default scheme for the MVC administration area.
builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BridgeAuthenticationHandler>(
        BridgeAuthenticationDefaults.Scheme,
        _ => { });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CakeOrders.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/AdminAuth/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(BridgeAuthenticationDefaults.Policy, policy =>
    {
        policy.AddAuthenticationSchemes(BridgeAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.GetUserAsync(context.Principal);

        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                IdentityConstants.ApplicationScheme);
        }
    };
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

// Attribute-routed API controllers (SadadBridge).
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (args.Any(x =>
    string.Equals(x, "--bootstrap-admin",
        StringComparison.OrdinalIgnoreCase)))
{
    await AdminBootstrapper.RunAsync(app.Services);
    return;
}

app.Run();
