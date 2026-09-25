using System.Diagnostics;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Features.Documents;

public static class UploadDocument
{
    public const long MaxBytes = 10_485_760;

    public sealed record Response(Guid Id, string FileName, long SizeBytes, int ChunkCount, DateTimeOffset UploadedAt);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/{id:guid}/documents", Handle);

    // AD-008: everything happens inside the request; nothing is persisted until every embedding exists.
    private static async Task<IResult> Handle(
        Guid id,
        HttpRequest request,
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IUsageRecorder usage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(UploadDocument));
        var started = Stopwatch.GetTimestamp();

        if (!await db.Organizations.AnyAsync(o => o.Id == id, ct))
            return Problems.OrganizationNotFound();

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

        var (document, failure) = await DocumentIngestion.PrepareAsync(db, embeddings, usage, UsageMode.Upload, id, Path.GetFileName(file.FileName), content, logger, ct);
        if (failure is not null)
            return failure;

        db.Documents.Add(document!);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DocumentIngestion.IsDuplicate(ex))
        {
            return DocumentIngestion.AlreadyAttached();
        }

        logger.LogInformation(
            "Document {DocumentId} ingested into organization {OrganizationId}: {ChunkCount} chunks in {ElapsedMs} ms",
            document!.Id, id, document.ChunkCount, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return TypedResults.Created(
            $"/api/organizations/{id}/documents/{document.Id}",
            new Response(document.Id, document.FileName, document.SizeBytes, document.ChunkCount, document.UploadedAt));
    }

    private static IResult FileRequired() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Envie um arquivo não vazio no campo 'file'."] });
}
