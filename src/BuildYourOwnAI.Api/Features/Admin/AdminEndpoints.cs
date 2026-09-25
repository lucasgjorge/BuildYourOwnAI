using BuildYourOwnAI.Api.Infrastructure;

namespace BuildYourOwnAI.Api.Features.Admin;

public static class AdminEndpoints
{
    // admin-usage door 4: every admin route requires the Admin role; a logged-in user without it gets 403.
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireRole(AdminRoleSeed.Role))
            .WithTags("Admin");
        GetUsage.Map(admin);
        return app;
    }
}
