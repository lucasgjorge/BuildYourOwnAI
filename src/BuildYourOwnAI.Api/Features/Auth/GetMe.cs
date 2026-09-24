using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Auth;

/// <summary>Who is logged in (user-name, door 3): Identity's manage/info has a fixed shape without the name.</summary>
public static class GetMe
{
    public sealed record Response(string? Email, string? FullName);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/me", Handle).RequireAuthorization();

    private static async Task<Results<Ok<Response>, ProblemHttpResult>> Handle(AppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var me = await db.Users
            .Where(u => u.Id == currentUser.Id)
            .Select(u => new Response(u.Email, u.FullName))
            .SingleOrDefaultAsync(ct);

        // A cookie can outlive its account; that session no longer identifies anyone.
        return me is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão inválida.")
            : TypedResults.Ok(me);
    }
}
