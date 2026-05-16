using IfsaKlasik.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IfsaKlasik.Web.Data;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";
    public string Email { get; set; } = "admin@ifsak.local";
    public string Password { get; set; } = "Admin123!";
}

public static class IdentitySeeder
{
    private static string AdminRoleName => IfsaRoles.Admin;

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var scoped = scope.ServiceProvider;
        var cfg = scoped.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        var roleManager = scoped.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scoped.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        }

        var email = cfg.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await EnsureRoles(userManager, existing);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var create = await userManager.CreateAsync(user, cfg.Password);
        if (!create.Succeeded)
        {
            var logger = scoped.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
            logger.LogError("Seed admin oluşturulamadı: {Errors}",
                string.Join(",", create.Errors.Select(e => $"{e.Code}:{e.Description}")));
            return;
        }

        await userManager.AddToRoleAsync(user, AdminRoleName);
    }

    private static async Task EnsureRoles(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, AdminRoleName))
            await userManager.AddToRoleAsync(user, AdminRoleName);
    }
}
