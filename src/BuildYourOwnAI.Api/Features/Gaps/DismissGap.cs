using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Gaps;

public static class DismissGap
{
    public static void Map(RouteGroupBuilder group) => group.MapPost("/{id:guid}/dismiss", Handle);

    private static async Task<IResult> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        var dismissed = await db.Gaps
            .Where(g => g.Id == id && g.Status == GapStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GapStatus.Dismissed), ct);
        if (dismissed > 0)
            return TypedResults.NoContent();

        return await db.Gaps.AnyAsync(g => g.Id == id, ct) ? GapsEndpoints.AlreadyClosed() : Problems.GapNotFound();
    }
}
