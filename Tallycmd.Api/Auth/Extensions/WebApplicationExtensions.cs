// ---------------------------
// Versión       : 0.0.0
// Copyright (C) : BeSS
// Clasificación : Restringida
// ---------------------------

namespace Tallycmd.Api.Auth.Extensions;

/// <summary>
/// Extension methods for startup-time administration tasks such as seeding
/// default roles and the initial admin user.
/// </summary>
public static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Seeds the roles defined in <see cref="Role.DefaultRoles"/> and creates
        /// a default admin user from configuration if one does not yet exist.
        /// The admin password must be supplied via user secrets or an environment
        /// variable under the key <c>Administration:DefaultAdmin:Password</c> —
        /// it is intentionally absent from <c>appsettings.json</c>.
        /// </summary>
        public async Task SeedDefaultRolesAndAdminAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Administration.Seed");

            var adminSection = configuration.GetRequiredSection("Administration:DefaultAdmin");
            var email = adminSection["Email"]
                ?? throw new InvalidOperationException(
                    "Administration:DefaultAdmin:Email is required.");
            var password = adminSection["Password"]
                ?? throw new InvalidOperationException(
                    "Administration:DefaultAdmin:Password is required. Configure it via user secrets or environment variables.");

            await CreateDefaultRolesAsync(roleManager, logger);

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is null)
            {
                await CreateDefaultAdminAsync(email, password, userManager, logger);
            }
            else
            {
                // Reset password on every startup so the configured value is always authoritative.
                // This ensures access is not lost when database copies are shared across environments.
                await UpdateDefaultAdminPasswordAsync(existingUser, password, userManager, logger);
            }
        }
    }

    private static async Task CreateDefaultRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in Role.DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                {
                    logger.LogInformation("Created role '{Role}'.", role);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }

    private static async Task CreateDefaultAdminAsync(string email, string password, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var adminUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create default admin user '{email}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var roleResult = await userManager.AddToRoleAsync(adminUser, Role.Admin);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{Role.Admin}' to default admin user '{email}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }

        logger.LogInformation(
            "Default admin user '{Email}' created and assigned to role '{Role}'.",
            email, Role.Admin);
    }

    private static async Task UpdateDefaultAdminPasswordAsync(ApplicationUser existingUser, string password, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(existingUser);
        var resetResult = await userManager.ResetPasswordAsync(existingUser, token, password);
        if (!resetResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update password for admin user '{existingUser.Email}': {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
        }

        logger.LogInformation(
            "Default admin user '{Email}' password synchronised on startup.",
            existingUser.Email);
    }
}
