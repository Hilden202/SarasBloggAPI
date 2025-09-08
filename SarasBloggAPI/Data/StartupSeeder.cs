using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace SarasBloggAPI.Data
{
    public static class StartupSeeder
    {
        public static async Task CreateAdminUserAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var roles = new[] { "user", "superuser", "admin", "superadmin" };
            foreach (var r in roles)
                if (!await roleManager.RoleExistsAsync(r))
                    await roleManager.CreateAsync(new IdentityRole(r));

            string adminEmail = config["AdminUser:Email"] ?? throw new InvalidOperationException("AdminUser:Email missing");
            string adminPassword = config["AdminUser:Password"] ?? throw new InvalidOperationException("AdminUser:Password missing");

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                    throw new Exception("Misslyckades skapa admin-användaren: " + string.Join(", ", result.Errors.Select(e => e.Description)));

                await userManager.AddToRoleAsync(adminUser, "superadmin");
            }
        }
    }
}
