using System.Text;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Ask;

public static class AskAssistant
{
    public const string RateLimitPolicy = "ask";
    public const int QuestionMaxLength = 2000;
    private const int TopK = 5;
    private const int ExcerptLength = 300;

    public sealed record Request(string? Question);

    public sealed record Source(Guid DocumentId, string FileName, int ChunkIndex, string Excerpt);

    public sealed record Response(string Answer, IReadOnlyList<Source> Sources);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/ask", Handle).RequireRateLimiting(RateLimitPolicy);

    private static async Task<IResult> Handle(
        Guid id,
        Request request,
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IChatClient chat,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(AskAssistant));
        var question = request.Question?.Trim() ?? "";
        if (question.Length is 0 or > QuestionMaxLength)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["question"] = [$"A pergunta deve ter entre 1 e {QuestionMaxLength} caracteres."],
            });

        var assistant = await db.Assistants
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Name, a.Instructions })
            .FirstOrDefaultAsync(ct);
        if (assistant is null)
            return Problems.AssistantNotFound();

        var (questionVector, embedFailure) = await AiProviderCall.TryAsync(
            async () => new Vector((await embeddings.GenerateAsync([question], cancellationToken: ct))[0].Vector),
            logger, "embed-question");
        if (embedFailure is not null)
            return embedFailure;

        var chunks = await RetrieveAsync(db, assistant.Id, questionVector!, ct);

        var messages = BuildPrompt(assistant.Name, assistant.Instructions, chunks, question);
        var (answer, chatFailure) = await AiProviderCall.TryAsync(
            () => chat.GetResponseAsync(messages, cancellationToken: ct), logger, "chat");
        if (chatFailure is not null)
            return chatFailure;

        var sources = chunks
            .Select(c => new Source(c.DocumentId, c.FileName, c.Index, c.Content.Length <= ExcerptLength ? c.Content : c.Content[..ExcerptLength]))
            .ToList();
        return TypedResults.Ok(new Response(answer!.Text, sources));
    }

    private sealed record RetrievedChunk(Guid DocumentId, string FileName, int Index, string Content);

    private static async Task<List<RetrievedChunk>> RetrieveAsync(AppDbContext db, Guid assistantId, Vector question, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The HNSW index filters after the scan; iterative scanning keeps going until TopK rows of THIS assistant
        // are found instead of returning fewer when other assistants' chunks fill the candidate list.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = strict_order", ct);

        // Door 5: only chunks of the assistant already resolved through the owner filter.
        var chunks = await db.Chunks
            .Where(c => c.Document.AssistantId == assistantId)
            .OrderBy(c => c.Embedding.CosineDistance(question))
            .Take(TopK)
            .Select(c => new RetrievedChunk(c.DocumentId, c.Document.FileName, c.Index, c.Content))
            .ToListAsync(ct);

        await transaction.CommitAsync(ct);
        return chunks;
    }

    private static List<ChatMessage> BuildPrompt(string name, string? instructions, List<RetrievedChunk> chunks, string question)
    {
        var system = new StringBuilder()
            .AppendLine($"Você é \"{name}\", um assistente que responde usando somente os trechos de documentos fornecidos.")
            .AppendLine("Se os trechos não contiverem a resposta, diga que não encontrou essa informação nos documentos.")
            .AppendLine("Cite o nome do arquivo de onde tirou cada informação.");
        if (!string.IsNullOrWhiteSpace(instructions))
            system.AppendLine().AppendLine("Instruções do dono deste assistente:").AppendLine(instructions);

        var user = new StringBuilder();
        if (chunks.Count == 0)
        {
            user.AppendLine("Nenhum documento foi anexado a este assistente.");
        }
        else
        {
            user.AppendLine("Trechos dos documentos:");
            for (var i = 0; i < chunks.Count; i++)
                user.AppendLine($"[{i + 1}] ({chunks[i].FileName}) {chunks[i].Content}");
        }
        user.AppendLine().AppendLine("Pergunta:").AppendLine(question);

        return [new ChatMessage(ChatRole.System, system.ToString()), new ChatMessage(ChatRole.User, user.ToString())];
    }
}
