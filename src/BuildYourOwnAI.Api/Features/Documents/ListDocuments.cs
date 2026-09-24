using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Documents;

public static class ListDocuments
{
    public sealed record Item(Guid Id, string FileName, long SizeBytes, int ChunkCount, DateTimeOffset UploadedAt);

    public static void Map(RouteGroupBuilder group) => group.MapGet("/{id:guid}/documents", Handle);

    private static async Task<IResult> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Assistants.AnyAsync(a => a.Id == id, ct))
            return Problems.AssistantNotFound();

        var items = await db.Documents
            .Where(d => d.AssistantId == id)
            .OrderByDescending(d => d.UploadedAt).ThenByDescending(d => d.Id)
            .Select(d => new Item(d.Id, d.FileName, d.SizeBytes, d.ChunkCount, d.UploadedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(items);
    }
}
