using System.Text.Json.Serialization;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace BuildYourOwnAI.Api.Features.Jev;

/// <summary>Jev: picks which of the user's assistants answers a question, then asks it through the regular ask pipeline.</summary>
public static class JevAsk
{
    private const int MaxOptions = 3;
    private const int MaxFallbackCandidates = 5;
    private const string NoneKey = "nenhuma";
    private const string Instructions = "Qual IA deve responder a esta pergunta do usuário?";
    private const string NoneDescription = "Nenhuma dessas IAs: a pergunta é sobre outro assunto";

    public sealed record Request(string? Question);

    public sealed record AssistantRef(Guid Id, string Name, string OrganizationName);

    // jev-gaps door 5: one shape, discriminated by Kind ∈ {answered, clarify, noMatch}.
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

    /// <summary>Jev's pick: an assistant (null for "none of them"), how sure it is, and every assistant by probability.</summary>
    private sealed record Decision(Eligible? Chosen, bool Confident, List<Eligible> ByProbability);

    public static IEndpointRouteBuilder MapJevEndpoints(this IEndpointRouteBuilder app)
    {
        // Global Jev: routes among all of the user's assistants.
        app.MapPost("/api/jev/ask", (
                Request request, AppDbContext db, ICurrentUser currentUser,
                IEmbeddingGenerator<string, Embedding<float>> embeddings, IChatClient chat, IJevChoice jev,
                IOptions<AiOptions> ai, ILoggerFactory loggerFactory, CancellationToken ct) =>
                Handle(null, request, db, currentUser, embeddings, chat, jev, ai.Value.Jev, loggerFactory, ct))
            .RequireAuthorization()
            .RequireRateLimiting(AskPipeline.RateLimitPolicy)
            .WithTags("Jev");

        // Door 1 (org-chat): the organization's Jev routes only among that organization's assistants.
        app.MapPost("/api/organizations/{id:guid}/jev/ask", (
                Guid id, Request request, AppDbContext db, ICurrentUser currentUser,
                IEmbeddingGenerator<string, Embedding<float>> embeddings, IChatClient chat, IJevChoice jev,
                IOptions<AiOptions> ai, ILoggerFactory loggerFactory, CancellationToken ct) =>
                Handle(id, request, db, currentUser, embeddings, chat, jev, ai.Value.Jev, loggerFactory, ct))
            .RequireAuthorization()
            .RequireRateLimiting(AskPipeline.RateLimitPolicy)
            .WithTags("Jev");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid? organizationId,
        Request request,
        AppDbContext db,
        ICurrentUser currentUser,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IChatClient chat,
        IJevChoice jev,
        AiOptions.JevSection settings,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(JevAsk));
        var question = request.Question?.Trim() ?? "";
        if (AskPipeline.InvalidQuestion(question) is { } invalid)
            return invalid;

        if (organizationId is { } orgId && !await db.Organizations.AnyAsync(o => o.Id == orgId, ct))
            return Problems.OrganizationNotFound();

        var eligible = await db.Assistants
            .Where(a => organizationId == null || a.OrganizationId == organizationId)
            .Where(a => a.RoutingDescription != null && a.RoutingDescription.Trim() != "")
            .OrderBy(a => a.Name).ThenBy(a => a.Id)
            .Select(a => new Eligible(a.Id, a.OrganizationId, a.Organization.Name, a.Name, a.Instructions, a.RoutingDescription!))
            .ToListAsync(ct);
        if (eligible.Count == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: organizationId is null
                    ? "Nenhuma IA disponível para o Jev. Preencha 'Quando usar esta IA' em pelo menos uma IA."
                    : "Nenhuma IA desta organização está disponível para o Jev. Preencha 'Quando usar esta IA' em pelo menos uma.");

        var decision = await RouteAsync(jev, settings, eligible, question, logger, ct);
        if (decision is null)
            return Outcome(logger, eligible.Count, new Response("clarify", Candidates: Refs(eligible, MaxFallbackCandidates)));

        if (!decision.Confident)
            return Outcome(logger, eligible.Count, new Response("clarify", Candidates: Refs(decision.ByProbability, MaxOptions)));

        if (decision.Chosen is not { } chosen)
        {
            // A question the organization's Jev could not route still belongs to that organization.
            await GapRecorder.RecordAsync(db, currentUser.Id!, organizationId, null, question, logger, ct);
            return Outcome(logger, eligible.Count, new Response("noMatch"));
        }

        var (answer, failure) = await AskPipeline.AnswerAsync(
            db, new AskPipeline.Target(chosen.Id, chosen.OrganizationId, chosen.Name, chosen.Instructions),
            question, currentUser.Id!, embeddings, chat, logger, ct);
        if (failure is not null)
            return failure;

        return Outcome(logger, eligible.Count, new Response(
            "answered", chosen.Ref, answer!.Text, answer.Found, answer.Sources,
            Alternatives: Refs(decision.ByProbability.Where(e => e != chosen), MaxOptions)));
    }

    private static List<AssistantRef> Refs(IEnumerable<Eligible> eligible, int max) => eligible.Take(max).Select(e => e.Ref).ToList();

    private static IResult Outcome(ILogger logger, int eligibleCount, Response response)
    {
        logger.LogInformation("Jev returned {Kind} among {EligibleCount} eligible assistants", response.Kind, eligibleCount);
        return TypedResults.Ok(response);
    }

    /// <summary>Asks Jev; null when the call failed or it picked a key it was not given (fallback to clarify).</summary>
    private static async Task<Decision?> RouteAsync(
        IJevChoice jev, AiOptions.JevSection settings, List<Eligible> eligible, string question, ILogger logger, CancellationToken ct)
    {
        // Door 2 (jev-choice): options are keyed by position, never by id, plus an explicit "none of them".
        // Only names, organizations and "when to use" go out: never instructions or documents.
        var criteria = eligible
            .Select((e, i) => (Key: (i + 1).ToString(), Text: $"{e.Name} (organização: {e.OrganizationName}): {e.RoutingDescription}"))
            .ToDictionary(o => o.Key, o => o.Text);
        criteria[NoneKey] = NoneDescription;

        JevChoice choice;
        try
        {
            choice = await jev.ChooseAsync(question, Instructions, criteria, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Jev router call failed with {ExceptionType}", ex.GetType().Name);
            return null;
        }

        Eligible? chosen = null;
        if (choice.Choice != NoneKey)
        {
            if (!int.TryParse(choice.Choice, out var index) || index < 1 || index > eligible.Count || choice.Choice != index.ToString())
            {
                logger.LogWarning("Jev router picked an option it was not given");
                return null;
            }
            chosen = eligible[index - 1];
        }

        var byProbability = eligible
            .Select((e, i) => (Eligible: e, Probability: choice.Probabilities.GetValueOrDefault((i + 1).ToString())))
            .OrderByDescending(p => p.Probability)
            .Select(p => p.Eligible)
            .ToList();
        return new Decision(chosen, choice.Confidence >= settings.ConfidenceThreshold, byProbability);
    }
}
