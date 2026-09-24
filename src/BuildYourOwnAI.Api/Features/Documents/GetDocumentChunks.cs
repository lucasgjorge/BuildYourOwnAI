using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Documents;

/// <summary>A chunk the chat cited, with its neighbors for context: what the preview panel shows.</summary>
public static class GetDocumentChunks
{
    private const int MaxAround = 2;

    public sealed record Item(int Index, string Content);

    public sealed record Response(Guid DocumentId, string FileName, int ChunkCount, IReadOnlyList<Item> Chunks);

    // Door 1 (source-preview): addressed by document and chunk index, the pair every answer's sources already carry.
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/documents/{documentId:guid}/chunks/{index:int}", Handle);

    private static async Task<IResult> Handle(Guid id, Guid documentId, int index, int? around, AppDbContext db, CancellationToken ct)
    {
        var span = around ?? 1;
        if (span is < 0 or > MaxAround)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["around"] = [$"'around' deve ficar entre 0 e {MaxAround}."],
            });

        if (!await db.Organizations.AnyAsync(o => o.Id == id, ct))
            return Problems.OrganizationNotFound();

        var document = await db.Documents
            .Where(d => d.Id == documentId && d.OrganizationId == id)
            .Select(d => new { d.FileName, d.ChunkCount })
            .FirstOrDefaultAsync(ct);
        if (document is null)
            return Problems.DocumentNotFound();
        if (index < 0 || index >= document.ChunkCount)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Trecho não encontrado.");

        var chunks = await db.Chunks
            .Where(c => c.DocumentId == documentId && c.Index >= index - span && c.Index <= index + span)
            .OrderBy(c => c.Index)
            .Select(c => new Item(c.Index, c.Content))
            .ToListAsync(ct);

        return TypedResults.Ok(new Response(documentId, document.FileName, document.ChunkCount, chunks));
    }
}
