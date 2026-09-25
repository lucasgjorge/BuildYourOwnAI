using System.Text;
using System.Text.Json;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Features.Study;

/// <summary>Samples chunks of the chosen documents and asks the model for one multiple-choice question per chunk.</summary>
public static class CreateStudySession
{
    public sealed record Request(IReadOnlyList<Guid>? DocumentIds, int? QuestionCount);

    // study-mode door 1: the answer key, the explanation and the source never go out before the answer.
    public sealed record Question(Guid Id, int Position, string Prompt, IReadOnlyList<string> Options);

    public sealed record Response(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<Question> Questions);

    private sealed record SampledChunk(Guid DocumentId, int Index, string Content);

    private sealed record Generated(int Chunk, string Prompt, List<string> Options, int Correct, string Explanation);

    public static void Map(RouteGroupBuilder organizations) =>
        organizations.MapPost("/{id:guid}/study-sessions", Handle).RequireRateLimiting(AskPipeline.RateLimitPolicy);

    private static async Task<IResult> Handle(
        Guid id, Request request, AppDbContext db, IChatClient chat, IUsageRecorder usage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(CreateStudySession));
        var errors = new Dictionary<string, string[]>();
        if (request.QuestionCount is not { } count || !StudySession.AllowedQuestionCounts.Contains(count))
            errors["questionCount"] = ["Escolha 5, 10 ou 20 perguntas."];
        if (request.DocumentIds is { Count: 0 })
            errors["documentIds"] = ["Escolha pelo menos um documento."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Organizations.AnyAsync(o => o.Id == id, ct))
            return Problems.OrganizationNotFound();

        var documentIds = request.DocumentIds?.Distinct().ToList();
        if (documentIds is not null
            && await db.Documents.CountAsync(d => d.OrganizationId == id && documentIds.Contains(d.Id), ct) != documentIds.Count)
            return Problems.DocumentNotFound();

        // One question per chunk, never the same chunk twice in a session.
        var chunks = await db.Chunks
            .Where(c => c.Document.OrganizationId == id && (documentIds == null || documentIds.Contains(c.DocumentId)))
            .OrderBy(_ => EF.Functions.Random())
            .Take(request.QuestionCount!.Value)
            .Select(c => new SampledChunk(c.DocumentId, c.Index, c.Content))
            .ToListAsync(ct);
        if (chunks.Count == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Os documentos escolhidos não têm texto para gerar perguntas.");

        var (reply, failure) = await AiProviderCall.TryAsync(
            () => chat.GetResponseAsync(BuildPrompt(chunks), new ChatOptions { ResponseFormat = ChatResponseFormat.Json }, ct),
            logger, "study-questions");
        if (failure is not null)
            return failure;
        await usage.RecordAsync(UsageMode.Study, UsageOperation.Chat, chat.ModelName(), reply!.Usage, ct);

        var generated = Parse(reply.Text, chunks.Count);
        if (generated.Count == 0)
        {
            logger.LogWarning("Study generation returned no valid question for {ChunkCount} chunks", chunks.Count);
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Não foi possível gerar perguntas. Tente novamente.");
        }

        var session = new StudySession { OrganizationId = id, QuestionCount = request.QuestionCount.Value };
        session.Questions.AddRange(generated.Select((g, i) => Shuffled(g, chunks[g.Chunk - 1], i + 1)));
        db.StudySessions.Add(session);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Study session {SessionId} created with {QuestionCount} questions", session.Id, session.Questions.Count);
        return TypedResults.Created(
            $"/api/study-sessions/{session.Id}",
            new Response(session.Id, session.CreatedAt,
                session.Questions.Select(q => new Question(q.Id, q.Position, q.Prompt, q.Options)).ToList()));
    }

    // study-mode door 3: the right option moves to a random position, and the key follows it.
    private static StudyQuestion Shuffled(Generated g, SampledChunk chunk, int position)
    {
        var order = Enumerable.Range(0, StudyQuestion.OptionCount).OrderBy(_ => Random.Shared.Next()).ToList();
        return new StudyQuestion
        {
            Position = position,
            DocumentId = chunk.DocumentId,
            ChunkIndex = chunk.Index,
            Prompt = g.Prompt,
            Options = order.Select(i => g.Options[i]).ToList(),
            CorrectOption = (short)order.IndexOf(g.Correct),
            Explanation = g.Explanation,
        };
    }

    // study-mode door 3: {"questions": [{"chunk", "prompt", "options"[4], "correct", "explanation"}]}; invalid items are dropped.
    private static List<Generated> Parse(string text, int chunkCount)
    {
        var valid = new List<Generated>();
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("questions", out var items) || items.ValueKind != JsonValueKind.Array)
                return valid;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("chunk", out var chunk) || !IsInt(chunk, out var chunkNumber)
                    || chunkNumber < 1 || chunkNumber > chunkCount || valid.Any(v => v.Chunk == chunkNumber)
                    || !item.TryGetProperty("prompt", out var prompt) || prompt.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(prompt.GetString())
                    || !item.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array
                    || options.GetArrayLength() != StudyQuestion.OptionCount
                    || !item.TryGetProperty("correct", out var correct) || !IsInt(correct, out var correctIndex)
                    || correctIndex is < 0 or >= StudyQuestion.OptionCount)
                    continue;

                var texts = options.EnumerateArray()
                    .Select(o => o.ValueKind == JsonValueKind.String ? o.GetString()!.Trim() : "")
                    .ToList();
                if (texts.Any(string.IsNullOrWhiteSpace) || texts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != texts.Count)
                    continue;

                var explanation = item.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : "";
                valid.Add(new Generated(chunkNumber, prompt.GetString()!.Trim(), texts, correctIndex, explanation));
            }
        }
        catch (JsonException)
        {
        }
        return valid;
    }

    // TryGetInt32 throws on a non-number (e.g. "chunk": "1"); a wrong type is just an invalid item.
    private static bool IsInt(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static List<ChatMessage> BuildPrompt(List<SampledChunk> chunks)
    {
        var system = new StringBuilder()
            .AppendLine("Gere perguntas de múltipla escolha para estudo, uma por trecho, usando só o conteúdo de cada trecho.")
            .AppendLine("Cada pergunta tem exatamente 4 alternativas diferentes, só uma certa, e uma explicação curta de por que ela é a certa.")
            .AppendLine("Escreva no mesmo idioma do trecho.")
            .AppendLine("Responda somente com JSON: {\"questions\": [{\"chunk\": <número do trecho>, \"prompt\": \"...\", \"options\": [\"...\", \"...\", \"...\", \"...\"], \"correct\": <0 a 3>, \"explanation\": \"...\"}]}.");

        var user = new StringBuilder().AppendLine("Trechos:");
        for (var i = 0; i < chunks.Count; i++)
            user.AppendLine($"[{i + 1}] {chunks[i].Content}");

        return [new ChatMessage(ChatRole.System, system.ToString()), new ChatMessage(ChatRole.User, user.ToString())];
    }
}
