using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class ListAssistants
{
    public sealed record Item(Guid Id, string Name, string? Instructions, DateTimeOffset CreatedAt, int DocumentCount);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/", Handle);

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var items = await db.Assistants
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new Item(a.Id, a.Name, a.Instructions, a.CreatedAt, a.Documents.Count))
            .ToListAsync(ct);

        return TypedResults.Ok(items);
    }
}
