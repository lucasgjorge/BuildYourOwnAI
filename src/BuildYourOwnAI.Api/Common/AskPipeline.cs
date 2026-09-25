using System.Text;
using System.Text.Json;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Common;

/// <summary>Retrieve -> answer -> open a gap when not found. Shared by a direct ask and by the automatic choice.</summary>
public static class AskPipeline
{
    public const string RateLimitPolicy = "ask";
    public const int QuestionMaxLength = 2000;
    private const int TopK = 5;
    private const int ExcerptLength = 300;

    public sealed record Source(Guid DocumentId, string FileName, int ChunkIndex, string Excerpt);

    public sealed record Answer(string Text, bool Found, IReadOnlyList<Source> Sources);

    /// <summary>The assistant a question goes to, already resolved through the owner filter.</summary>
    public sealed record Target(Guid Id, Guid OrganizationId, string Name, string? Instructions);

    public static ValidationProblem? InvalidQuestion(string question) =>
        question.Length is 0 or > QuestionMaxLength
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["question"] = [$"A pergunta deve ter entre 1 e {QuestionMaxLength} caracteres."],
            })
            : null;

    /// <summary>
    /// Retrieves from the assistant's organization, answers, and opens a gap when the answer was not found.
    /// <paramref name="previousQuestion"/> is the thread's previous question: it steers retrieval and the answer of a
    /// follow-up, but only <paramref name="question"/> becomes a gap.
    /// </summary>
    public static async Task<(Answer? Answer, IResult? Failure)> AnswerAsync(
        AppDbContext db,
        Target assistant,
        string question,
        string ownerId,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IChatClient chat,
        IUsageRecorder usage,
        UsageMode mode,
        ILogger logger,
        CancellationToken ct,
        string? previousQuestion = null)
    {
        var searchText = previousQuestion is null ? question : $"{previousQuestion}\n{question}";
        var (generated, embedFailure) = await AiProviderCall.TryAsync(
            () => embeddings.GenerateAsync([searchText], cancellationToken: ct), logger, "embed-question");
        if (embedFailure is not null)
            return (null, embedFailure);
        await usage.RecordAsync(mode, UsageOperation.Embedding, embeddings.ModelName(), generated!.Usage, ct);
        var questionVector = new Vector(generated[0].Vector);

        var chunks = await RetrieveAsync(db, assistant.OrganizationId, questionVector, ct);

        var messages = BuildPrompt(assistant.Name, assistant.Instructions, chunks, question, previousQuestion);
        var (reply, chatFailure) = await AiProviderCall.TryAsync(
            () => chat.GetResponseAsync(messages, new ChatOptions { ResponseFormat = ChatResponseFormat.Json }, ct), logger, "chat");
        if (chatFailure is not null)
            return (null, chatFailure);
        await usage.RecordAsync(mode, UsageOperation.Chat, chat.ModelName(), reply!.Usage, ct);

        var (answer, found) = ParseReply(reply.Text);
        if (!found)
            await GapRecorder.RecordAsync(db, ownerId, assistant.OrganizationId, assistant.Id, question, logger, ct);

        var sources = chunks
            .Select(c => new Source(c.DocumentId, c.FileName, c.Index, c.Content.Length <= ExcerptLength ? c.Content : c.Content[..ExcerptLength]))
            .ToList();
        return (new Answer(answer, found, sources), null);
    }

    // Door 6: {"answer": string, "found": bool}. Anything else counts as answered, so a malformed reply never opens a gap.
    private static (string Answer, bool Found) ParseReply(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String
                && root.TryGetProperty("found", out var found) && found.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return (answer.GetString()!, found.GetBoolean());
        }
        catch (JsonException)
        {
        }
        return (text, true);
    }

    private sealed record RetrievedChunk(Guid DocumentId, string FileName, int Index, string Content);

    private static async Task<List<RetrievedChunk>> RetrieveAsync(AppDbContext db, Guid organizationId, Vector question, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The HNSW index filters after the scan; iterative scanning keeps going until TopK rows of THIS organization
        // are found instead of returning fewer when other organizations' chunks fill the candidate list.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = strict_order", ct);

        // Door 1: only chunks of the organization of an assistant already resolved through the owner filter.
        var chunks = await db.Chunks
            .Where(c => c.Document.OrganizationId == organizationId)
            .OrderBy(c => c.Embedding.CosineDistance(question))
            .Take(TopK)
            .Select(c => new RetrievedChunk(c.DocumentId, c.Document.FileName, c.Index, c.Content))
            .ToListAsync(ct);

        await transaction.CommitAsync(ct);
        return chunks;
    }

    private static List<ChatMessage> BuildPrompt(string name, string? instructions, List<RetrievedChunk> chunks, string question, string? previousQuestion)
    {
        var system = new StringBuilder()
            .AppendLine($"Você é \"{name}\", um assistente que responde usando somente os trechos de documentos fornecidos.")
            .AppendLine("Se os trechos não contiverem a resposta, diga que não encontrou essa informação nos documentos.")
            .AppendLine("Cite o nome do arquivo de onde tirou cada informação.")
            .AppendLine("Responda somente com um objeto JSON: {\"answer\": \"<sua resposta>\", \"found\": <true|false>}.")
            .AppendLine("Use \"found\": false quando os trechos não contêm a resposta.");
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
        if (previousQuestion is not null)
            user.AppendLine().AppendLine("Pergunta anterior da conversa (só contexto):").AppendLine(previousQuestion);
        user.AppendLine().AppendLine("Pergunta:").AppendLine(question);

        return [new ChatMessage(ChatRole.System, system.ToString()), new ChatMessage(ChatRole.User, user.ToString())];
    }
}
