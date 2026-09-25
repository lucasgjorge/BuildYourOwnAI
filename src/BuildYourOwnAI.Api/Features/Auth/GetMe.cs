using System.Security.Claims;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Auth;

/// <summary>Who is logged in (user-name, door 3): Identity's manage/info has a fixed shape without the name.</summary>
public static class GetMe
{
    // admin-usage door 4: isAdmin comes from the session's roles, the same ones the admin routes check.
    public sealed record Response(string? Email, string? FullName, bool IsAdmin);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/me", Handle).RequireAuthorization();

    private static async Task<Results<Ok<Response>, ProblemHttpResult>> Handle(
        AppDbContext db, ICurrentUser currentUser, ClaimsPrincipal user, CancellationToken ct)
    {
        var me = await db.Users
            .Where(u => u.Id == currentUser.Id)
            .Select(u => new Response(u.Email, u.FullName, false))
            .SingleOrDefaultAsync(ct);

        // A cookie can outlive its account; that session no longer identifies anyone.
        return me is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão inválida.")
            : TypedResults.Ok(me with { IsAdmin = user.IsInRole(AdminRoleSeed.Role) });
    }
}
