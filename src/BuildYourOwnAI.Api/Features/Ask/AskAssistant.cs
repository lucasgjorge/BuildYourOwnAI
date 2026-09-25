using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Features.Ask;

public static class AskAssistant
{
    public sealed record Request(string? Question);

    public sealed record Response(string Answer, bool Found, IReadOnlyList<AskPipeline.Source> Sources);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/ask", Handle).RequireRateLimiting(AskPipeline.RateLimitPolicy);

    private static async Task<IResult> Handle(
        Guid id,
        Request request,
        AppDbContext db,
        ICurrentUser currentUser,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IChatClient chat,
        IUsageRecorder usage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(AskAssistant));
        var question = request.Question?.Trim() ?? "";
        if (AskPipeline.InvalidQuestion(question) is { } invalid)
            return invalid;

        var assistant = await db.Assistants
            .Where(a => a.Id == id)
            .Select(a => new AskPipeline.Target(a.Id, a.OrganizationId, a.Name, a.Instructions))
            .FirstOrDefaultAsync(ct);
        if (assistant is null)
            return Problems.AssistantNotFound();

        var (answer, failure) = await AskPipeline.AnswerAsync(db, assistant, question, currentUser.Id!, embeddings, chat, usage, UsageMode.Ask, logger, ct);
        return failure ?? TypedResults.Ok(new Response(answer!.Text, answer.Found, answer.Sources));
    }
}
