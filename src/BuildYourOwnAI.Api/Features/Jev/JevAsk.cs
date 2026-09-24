using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Features.Jev;

/// <summary>Jev: picks which of the user's assistants answers a question, then asks it through the regular ask pipeline.</summary>
public static class JevAsk
{
    private const int MaxOptions = 3;
    private const int MaxFallbackCandidates = 5;

    public sealed record Request(string? Question);

    public sealed record AssistantRef(Guid Id, string Name, string OrganizationName);

    // Door 5: one shape, discriminated by Kind ∈ {answered, clarify, noMatch}.
    public sealed record Response(
        string Kind,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AssistantRef? Assistant = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Answer = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Found = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AskPipeline.Source>? Sources = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AssistantRef>? Alternatives = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AssistantRef>? Candidates = null);

    private sealed record Eligible(Guid Id, Guid OrganizationId, string OrganizationName, string Name, string? Instructions, string RoutingDescription)
    {
        public AssistantRef Ref => new(Id, Name, OrganizationName);
    }

    private sealed record Decision(int? Choice, bool Confident);

    public static IEndpointRouteBuilder MapJevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jev/ask", Handle)
            .RequireAuthorization()
            .RequireRateLimiting(AskPipeline.RateLimitPolicy)
            .WithTags("Jev");
        return app;
    }

    private static async Task<IResult> Handle(
        Request request,
        AppDbContext db,
        ICurrentUser currentUser,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IChatClient chat,
        [FromKeyedServices(AiServiceCollectionExtensions.RouterKey)] IChatClient router,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(JevAsk));
        var question = request.Question?.Trim() ?? "";
        if (AskPipeline.InvalidQuestion(question) is { } invalid)
            return invalid;

        var eligible = await db.Assistants
            .Where(a => a.RoutingDescription != null && a.RoutingDescription.Trim() != "")
            .OrderBy(a => a.Name).ThenBy(a => a.Id)
            .Select(a => new Eligible(a.Id, a.OrganizationId, a.Organization.Name, a.Name, a.Instructions, a.RoutingDescription!))
            .ToListAsync(ct);
        if (eligible.Count == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Nenhuma IA disponível para o Jev. Preencha 'Quando usar esta IA' em pelo menos uma IA.");

        var decision = await RouteAsync(router, eligible, question, logger, ct);
        if (decision is null)
            return Outcome(logger, eligible.Count, new Response("clarify", Candidates: Refs(eligible, MaxFallbackCandidates)));

        if (decision.Choice is not { } choice)
        {
            if (!decision.Confident)
                return Outcome(logger, eligible.Count, new Response("clarify", Candidates: Refs(eligible, MaxOptions)));

            await GapRecorder.RecordAsync(db, currentUser.Id!, null, null, question, logger, ct);
            return Outcome(logger, eligible.Count, new Response("noMatch"));
        }

        var chosen = eligible[choice - 1];
        var others = eligible.Where(e => e != chosen).ToList();
        if (!decision.Confident)
            return Outcome(logger, eligible.Count, new Response("clarify", Candidates: [chosen.Ref, .. Refs(others, MaxOptions - 1)]));

        var (answer, failure) = await AskPipeline.AnswerAsync(
            db, new AskPipeline.Target(chosen.Id, chosen.OrganizationId, chosen.Name, chosen.Instructions),
            question, currentUser.Id!, embeddings, chat, logger, ct);
        if (failure is not null)
            return failure;

        return Outcome(logger, eligible.Count, new Response(
            "answered", chosen.Ref, answer!.Text, answer.Found, answer.Sources, Alternatives: Refs(others, MaxOptions)));
    }

    private static List<AssistantRef> Refs(IEnumerable<Eligible> eligible, int max) => eligible.Take(max).Select(e => e.Ref).ToList();

    private static IResult Outcome(ILogger logger, int eligibleCount, Response response)
    {
        logger.LogInformation("Jev returned {Kind} among {EligibleCount} eligible assistants", response.Kind, eligibleCount);
        return TypedResults.Ok(response);
    }

    /// <summary>Asks the router; null when it failed or replied with anything but a valid decision (fallback to clarify).</summary>
    private static async Task<Decision?> RouteAsync(
        IChatClient router, List<Eligible> eligible, string question, ILogger logger, CancellationToken ct)
    {
        string text;
        try
        {
            var reply = await router.GetResponseAsync(
                BuildPrompt(eligible, question), new ChatOptions { ResponseFormat = ChatResponseFormat.Json }, ct);
            text = reply.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Jev router call failed with {ExceptionType}", ex.GetType().Name);
            return null;
        }

        var decision = ParseDecision(text, eligible.Count);
        if (decision is null)
            logger.LogWarning("Jev router reply was not a valid decision");
        return decision;
    }

    // Door 5: the router answers with a 1-based index into the list it was shown, never an id.
    private static Decision? ParseDecision(string text, int count)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("confident", out var confident) || confident.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("choice", out var choice))
                return null;

            if (choice.ValueKind == JsonValueKind.Null)
                return new Decision(null, confident.GetBoolean());
            if (choice.ValueKind == JsonValueKind.Number && choice.TryGetInt32(out var index) && index >= 1 && index <= count)
                return new Decision(index, confident.GetBoolean());
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Only names and routing descriptions go to the router: never instructions or document content.
    private static List<ChatMessage> BuildPrompt(List<Eligible> eligible, string question)
    {
        var system = new StringBuilder()
            .AppendLine("Você é o Jev, um roteador. Escolha qual IA da lista deve responder à pergunta, pelo número dela.")
            .AppendLine("Responda somente com um objeto JSON: {\"choice\": <número da IA ou null>, \"confident\": <true|false>}.")
            .AppendLine("Use \"choice\": null quando nenhuma IA da lista serve para a pergunta.")
            .AppendLine("Use \"confident\": false quando mais de uma IA serviria igualmente ou você não tem certeza.");

        var user = new StringBuilder().AppendLine("IAs:");
        for (var i = 0; i < eligible.Count; i++)
            user.AppendLine($"{i + 1}. {eligible[i].Name} (organização: {eligible[i].OrganizationName}): {eligible[i].RoutingDescription}");
        user.AppendLine().AppendLine("Pergunta:").AppendLine(question);

        return [new ChatMessage(ChatRole.System, system.ToString()), new ChatMessage(ChatRole.User, user.ToString())];
    }
}
