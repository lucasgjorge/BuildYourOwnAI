using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace BuildYourOwnAI.Api.Infrastructure;

/// <summary>
/// Creates a ready-to-use account for local development from <c>DevSeed:AdminEmail</c> / <c>DevSeed:AdminPassword</c>.
/// Called only in the Development environment; never from a migration, which would also run in production.
/// </summary>
public static class DevAdminSeed
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["DevSeed:AdminEmail"];
        var password = configuration["DevSeed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var users = services.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(email) is not null)
            return;

        var result = await users.CreateAsync(new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Administrador" }, password);
        if (result.Succeeded)
            logger.LogInformation("Development account {Email} created", email);
        else
            logger.LogWarning("Development account was not created: {Errors}", string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
