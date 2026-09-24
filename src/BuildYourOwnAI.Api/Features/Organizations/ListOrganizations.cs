using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Organizations;

public static class ListOrganizations
{
    public sealed record Item(Guid Id, string Name, DateTimeOffset CreatedAt, int AssistantCount, int DocumentCount);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/", Handle);

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var items = await db.Organizations
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Select(o => new Item(o.Id, o.Name, o.CreatedAt, o.Assistants.Count, o.Documents.Count))
            .ToListAsync(ct);

        return TypedResults.Ok(items);
    }
}
