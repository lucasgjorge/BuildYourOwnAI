using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace BuildYourOwnAI.Api.Common;

/// <summary>
/// admin-usage door 2: called right after each successful AI call. Recording never fails the request.
/// </summary>
public interface IUsageRecorder
{
    Task RecordAsync(UsageMode mode, UsageOperation operation, string model, UsageDetails? usage, CancellationToken ct);
}

public sealed class UsageRecorder(
    ICurrentUser currentUser,
    IHttpContextAccessor httpContext,
    IServiceScopeFactory scopes,
    IOptions<AiOptions> ai,
    ILogger<UsageRecorder> logger) : IUsageRecorder
{
    public async Task RecordAsync(UsageMode mode, UsageOperation operation, string model, UsageDetails? usage, CancellationToken ct)
    {
        try
        {
            var input = Tokens(usage?.InputTokenCount);
            var output = Tokens(usage?.OutputTokenCount);
            var usageRow = new AiUsage
            {
                UserId = currentUser.Id ?? throw new InvalidOperationException("AI call without a logged-in user."),
                CorrelationId = CorrelationId.Get(httpContext.HttpContext) ?? Guid.NewGuid().ToString(),
                Mode = mode,
                Operation = operation,
                Model = model,
                InputTokens = input,
                OutputTokens = output,
                CostUsd = Cost(model, input, output),
            };

            // A scope of its own: the row is saved even when the request's context is inside a transaction that rolls back.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiUsages.Add(usageRow);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI usage of {Mode}/{Operation} was not recorded: {ExceptionType}", mode, operation, ex.GetType().Name);
        }
    }

    private decimal? Cost(string model, int input, int output) =>
        ai.Value.Pricing.TryGetValue(model, out var price)
            ? Math.Round((input * price.InputPerMillion + output * price.OutputPerMillion) / 1_000_000m, 6)
            : null;

    private static int Tokens(long? count) => (int)Math.Clamp(count ?? 0, 0, int.MaxValue);
}

public static class UsageModels
{
    // The configured model name (what AI:Pricing is keyed by), not the dated snapshot a response may report.
    public static string ModelName(this IChatClient chat) =>
        chat.GetService<ChatClientMetadata>()?.DefaultModelId ?? "unknown";

    public static string ModelName(this IEmbeddingGenerator<string, Embedding<float>> embeddings) =>
        embeddings.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId ?? "unknown";
}
