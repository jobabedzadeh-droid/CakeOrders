
using CakeOrders.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CakeOrders.Web.Identity;

public static class AdminBootstrapper
{
    private const string AdminRole = "Admin";
    private const string UserRole = "User";

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRoleAsync(roleManager, AdminRole);
        await EnsureRoleAsync(roleManager, UserRole);

        var existingAdmins = await userManager.GetUsersInRoleAsync(AdminRole);

        if (existingAdmins.Any())
        {
            Console.WriteLine("An administrator already exists. Bootstrap cancelled.");
            return;
        }

        Console.Write("Display name: ");
        var displayName = Console.ReadLine()?.Trim();

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("Display name and username are required.");
            return;
        }

        Console.Write("Password: ");
        var password = ReadHiddenPassword();

        Console.Write("\nConfirm password: ");
        var confirmation = ReadHiddenPassword();

        if (password != confirmation)
        {
            Console.WriteLine("\nPasswords do not match.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = username,
            DisplayName = displayName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            Console.WriteLine("\nUser creation failed:");

            foreach (var error in createResult.Errors)
                Console.WriteLine($"- {error.Description}");

            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, AdminRole);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            Console.WriteLine("\nRole assignment failed. Created user was rolled back.");

            foreach (var error in roleResult.Errors)
                Console.WriteLine($"- {error.Description}");

            return;
        }

        Console.WriteLine("\nAdministrator created successfully.");
        Console.WriteLine($"Username: {username}");
        Console.WriteLine("The password was not displayed or saved.");
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole> roleManager,
        string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var result = await roleManager.CreateAsync(
            new IdentityRole(roleName));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create role '{roleName}': " +
                string.Join(", ", result.Errors.Select(x => x.Description)));
        }
    }

    private static string ReadHiddenPassword()
    {
        var password = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                    password.Length--;

                continue;
            }

            if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }

        return password.ToString();
    }
}