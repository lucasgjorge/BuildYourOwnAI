using System.Diagnostics;
using System.Security.Cryptography;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Npgsql;
using Pgvector;

namespace BuildYourOwnAI.Api.Features.Documents;

public static class UploadDocument
{
    public const long MaxBytes = 10_485_760;
    private const int EmbeddingBatchSize = 100;

    public sealed record Response(Guid Id, string FileName, long SizeBytes, int ChunkCount, DateTimeOffset UploadedAt);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/{id:guid}/documents", Handle);

    // Door 8: everything happens inside the request; nothing is persisted until every embedding exists.
    private static async Task<IResult> Handle(
        Guid id,
        HttpRequest request,
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(UploadDocument));
        var started = Stopwatch.GetTimestamp();

        if (!await db.Assistants.AnyAsync(a => a.Id == id, ct))
            return Problems.AssistantNotFound();

        if (!request.HasFormContentType)
            return FileRequired();
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return FileRequired();
        if (!TextExtractor.IsSupported(file.FileName))
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: $"Formato não suportado. Use {string.Join(", ", TextExtractor.SupportedExtensions)}.");
        if (file.Length > MaxBytes)
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "O arquivo passa do limite de 10 MB.");

        byte[] content;
        using (var buffer = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        if (await db.Documents.AnyAsync(d => d.AssistantId == id && d.ContentSha256 == sha256, ct))
            return AlreadyAttached();

        var chunks = TextChunker.Split(TextExtractor.Extract(file.FileName, content));
        if (chunks.Count == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Não foi possível extrair texto do arquivo (PDF escaneado?).");

        var (vectors, failure) = await AiProviderCall.TryAsync(() => EmbedAsync(embeddings, chunks, ct), logger, "embed-document");
        if (failure is not null)
            return failure;

        var document = new Document
        {
            AssistantId = id,
            FileName = Path.GetFileName(file.FileName),
            SizeBytes = file.Length,
            ContentSha256 = sha256,
            ChunkCount = chunks.Count,
            Chunks = chunks.Select((text, i) => new Chunk { Index = i, Content = text, Embedding = vectors![i] }).ToList(),
        };
        db.Documents.Add(document);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two uploads of the same content raced past the check above; the unique index (door 3) decides.
            return AlreadyAttached();
        }

        logger.LogInformation(
            "Document {DocumentId} ingested into assistant {AssistantId}: {ChunkCount} chunks in {ElapsedMs} ms",
            document.Id, id, document.ChunkCount, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return TypedResults.Created(
            $"/api/assistants/{id}/documents/{document.Id}",
            new Response(document.Id, document.FileName, document.SizeBytes, document.ChunkCount, document.UploadedAt));
    }

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

    private static IResult FileRequired() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Envie um arquivo não vazio no campo 'file'."] });

    private static IResult AlreadyAttached() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Este arquivo já foi anexado a esta IA.");
}
