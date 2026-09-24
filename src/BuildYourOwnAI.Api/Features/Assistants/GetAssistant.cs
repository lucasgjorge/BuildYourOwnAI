using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class GetAssistant
{
    public sealed record Response(
        Guid Id,
        Guid OrganizationId,
        string OrganizationName,
        string Name,
        string? Instructions,
        string? RoutingDescription,
        DateTimeOffset CreatedAt);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/{id:guid}", Handle);

    private static async Task<Results<Ok<Response>, ProblemHttpResult>> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        var assistant = await db.Assistants
            .Where(a => a.Id == id)
            .Select(a => new Response(a.Id, a.OrganizationId, a.Organization.Name, a.Name, a.Instructions, a.RoutingDescription, a.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return assistant is null ? Problems.AssistantNotFound() : TypedResults.Ok(assistant);
    }
}
