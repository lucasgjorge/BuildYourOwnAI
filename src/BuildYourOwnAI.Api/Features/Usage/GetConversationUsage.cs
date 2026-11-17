using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Usage;

public static class GetConversationUsage
{
    public sealed record Response(long InputTokens, long OutputTokens, long TotalTokens);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/usage/conversations/{conversationId:guid}", Handle)
            .RequireAuthorization()
            .WithTags("Usage");

    private static async Task<IResult> Handle(
        Guid conversationId,
        AppDbContext db,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var totals = await db.AiUsages
            .Where(u => u.UserId == currentUser.Id && u.ConversationId == conversationId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                InputTokens = g.Sum(u => (long)u.InputTokens),
                OutputTokens = g.Sum(u => (long)u.OutputTokens),
            })
            .FirstOrDefaultAsync(ct);

        var input = totals?.InputTokens ?? 0;
        var output = totals?.OutputTokens ?? 0;
        return TypedResults.Ok(new Response(input, output, input + output));
    }
}
