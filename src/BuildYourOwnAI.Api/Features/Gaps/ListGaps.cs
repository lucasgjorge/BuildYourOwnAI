using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Gaps;

public static class ListGaps
{
    public sealed record Ref(Guid Id, string Name);

    public sealed record Item(
        Guid Id, string Question, int AskCount, DateTimeOffset FirstAskedAt, DateTimeOffset LastAskedAt, Ref? Organization, Ref? Assistant);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/", Handle);

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var items = await db.Gaps
            .Where(g => g.Status == GapStatus.Open)
            .OrderByDescending(g => g.AskCount).ThenByDescending(g => g.LastAskedAt).ThenByDescending(g => g.Id)
            .Select(g => new Item(
                g.Id,
                g.Question,
                g.AskCount,
                g.FirstAskedAt,
                g.LastAskedAt,
                g.Organization == null ? null : new Ref(g.Organization.Id, g.Organization.Name),
                g.Assistant == null ? null : new Ref(g.Assistant.Id, g.Assistant.Name)))
            .ToListAsync(ct);

        return TypedResults.Ok(items);
    }
}
