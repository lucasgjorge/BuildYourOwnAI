using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace BuildYourOwnAI.Api.Infrastructure;

/// <summary>
/// admin-usage door 3: the Identity role <see cref="Role"/> exists, and the accounts in <c>Admin:Emails</c> (plus, in
/// Development, <c>DevSeed:AdminEmail</c>) have it. Runs on every startup; an account created later gets it on the next one.
/// </summary>
public static class AdminRoleSeed
{
    public const string Role = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, bool includeDevAccount, ILogger logger)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(Role))
            await roles.CreateAsync(new IdentityRole(Role));

        var emails = configuration.GetSection("Admin:Emails").Get<string[]>() ?? [];
        if (includeDevAccount && configuration["DevSeed:AdminEmail"] is { } devEmail)
            emails = [.. emails, devEmail];

        var users = services.GetRequiredService<UserManager<AppUser>>();
        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await users.FindByEmailAsync(email) is not { } user || await users.IsInRoleAsync(user, Role))
                continue;
            var result = await users.AddToRoleAsync(user, Role);
            if (result.Succeeded)
                logger.LogInformation("Account {UserId} received the {Role} role", user.Id, Role);
            else
                logger.LogWarning("Account {UserId} did not receive the {Role} role: {Errors}", user.Id, Role, string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }
}
