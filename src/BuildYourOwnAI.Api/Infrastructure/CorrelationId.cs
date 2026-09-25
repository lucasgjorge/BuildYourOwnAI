using BuildYourOwnAI.Api.Infrastructure.Data;

namespace BuildYourOwnAI.Api.Infrastructure;

/// <summary>
/// admin-usage door 1: one id per request, from <c>X-Correlation-ID</c> or a new GUID, echoed on the response.
/// Every AI call of the request is recorded under it, so the report counts actions, not calls.
/// </summary>
public static class CorrelationId
{
    public const string Header = "X-Correlation-ID";
    private static readonly object ItemKey = new();

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = incoming.Length is > 0 and <= AiUsage.CorrelationIdMaxLength && !string.IsNullOrWhiteSpace(incoming)
            ? incoming
            : Guid.NewGuid().ToString();
        context.Items[ItemKey] = id;
        context.Response.Headers[Header] = id;
        return next(context);
    });

    public static string? Get(HttpContext? context) => context?.Items[ItemKey] as string;
}
