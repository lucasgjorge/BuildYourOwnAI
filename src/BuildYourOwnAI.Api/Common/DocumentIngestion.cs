using System.Security.Cryptography;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Npgsql;
using Pgvector;

namespace BuildYourOwnAI.Api.Common;

/// <summary>Turns file content into a document of an organization: used by the upload and by answering a gap.</summary>
public static class DocumentIngestion
{
    private const int EmbeddingBatchSize = 100;

    /// <summary>
    /// Hashes, extracts, chunks and embeds <paramref name="content"/> into an unsaved <see cref="Document"/> of the
    /// organization. The caller adds it and saves, so it can close other work in the same transaction.
    /// </summary>
    public static async Task<(Document? Document, IResult? Failure)> PrepareAsync(
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        Guid organizationId,
        string fileName,
        byte[] content,
        ILogger logger,
        CancellationToken ct)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        if (await db.Documents.AnyAsync(d => d.OrganizationId == organizationId && d.ContentSha256 == sha256, ct))
            return (null, AlreadyAttached());

        var chunks = TextChunker.Split(TextExtractor.Extract(fileName, content));
        if (chunks.Count == 0)
            return (null, Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Não foi possível extrair texto do arquivo (PDF escaneado?)."));

        var (vectors, failure) = await AiProviderCall.TryAsync(() => EmbedAsync(embeddings, chunks, ct), logger, "embed-document");
        if (failure is not null)
            return (null, failure);

        return (new Document
        {
            OrganizationId = organizationId,
            FileName = fileName,
            SizeBytes = content.Length,
            ContentSha256 = sha256,
            ChunkCount = chunks.Count,
            Chunks = chunks.Select((text, i) => new Chunk { Index = i, Content = text, Embedding = vectors![i] }).ToList(),
        }, null);
    }

    // Two uploads of the same content raced past the check in PrepareAsync; the unique index (door 1) decides.
    public static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static IResult AlreadyAttached() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Este arquivo já foi anexado a esta organização.");

    private static async Task<List<Vector>> EmbedAsync(
        IEmbeddingGenerator<string, Embedding<float>> embeddings, IReadOnlyList<string> chunks, CancellationToken ct)
    {
        var vectors = new List<Vector>(chunks.Count);
        foreach (var batch in chunks.Chunk(EmbeddingBatchSize))
        {
            var generated = await embeddings.GenerateAsync(batch, cancellationToken: ct);
            vectors.AddRange(generated.Select(e => new Vector(e.Vector)));
        }
        return vectors;
    }
}
