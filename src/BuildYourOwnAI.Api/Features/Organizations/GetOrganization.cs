using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Organizations;

public static class GetOrganization
{
    public sealed record AssistantItem(Guid Id, string Name, string? RoutingDescription);

    public sealed record Response(Guid Id, string Name, DateTimeOffset CreatedAt, int DocumentCount, IReadOnlyList<AssistantItem> Assistants);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/{id:guid}", Handle);

    private static async Task<Results<Ok<Response>, ProblemHttpResult>> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        var organization = await db.Organizations
            .Where(o => o.Id == id)
            .Select(o => new Response(
                o.Id,
                o.Name,
                o.CreatedAt,
                o.Documents.Count,
                o.Assistants
                    .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                    .Select(a => new AssistantItem(a.Id, a.Name, a.RoutingDescription))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return organization is null ? Problems.OrganizationNotFound() : TypedResults.Ok(organization);
    }
}
