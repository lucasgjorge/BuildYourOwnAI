using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Infrastructure.Ai;

/// <summary>
/// What was picked among the options given, how sure it is, and the distribution over all options; plus the model
/// and the tokens the provider reported, for the usage record (admin-usage).
/// </summary>
public sealed record RoutingChoice(
    string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities, string Model = "unknown", UsageDetails? Usage = null);

/// <summary>
/// AD-012: the "choice" primitive behind the automatic choice of assistant. Handlers see only this; the HTTP contract of
/// the provider stays in <see cref="OpenRouterRoutingChoice"/>.
/// </summary>
public interface IRoutingChoice
{
    Task<RoutingChoice> ChooseAsync(string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct);
}

/// <summary>
/// TypeSafe "System One" via OpenRouter: <c>POST {base}systemone</c>, not the chat completions endpoint.
/// The model is the provider's bare model name, from configuration.
/// </summary>
public sealed class OpenRouterRoutingChoice(HttpClient http, string model) : IRoutingChoice
{
    private const string QuestionKey = "principal";

    public async Task<RoutingChoice> ChooseAsync(
        string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct)
    {
        var request = new Request(model, state, new Dictionary<string, Question>
        {
            [QuestionKey] = new("choice", instructions, criteria),
        });

        using var response = await http.PostAsJsonAsync("systemone", request, ct);
        // The provider's body may echo the question; it never goes into the exception.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Routing choice returned {(int)response.StatusCode}.", null, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response>(ct);
        if (body?.Answers is null || !body.Answers.TryGetValue(QuestionKey, out var answer) || answer.Choice is null)
            throw new InvalidOperationException("Routing choice response has no answer for the question.");

        // OpenRouter reports usage in the OpenAI shape; a response without it is recorded with zero tokens.
        var usage = body.Usage is { } u ? new UsageDetails { InputTokenCount = u.PromptTokens, OutputTokenCount = u.CompletionTokens } : null;
        return new RoutingChoice(answer.Choice, answer.Confidence, answer.Probabilities ?? new Dictionary<string, double>(), model, usage);
    }

    private sealed record Request(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("questions")] IReadOnlyDictionary<string, Question> Questions);

    private sealed record Question(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("instructions")] string Instructions,
        [property: JsonPropertyName("criteria")] IReadOnlyDictionary<string, string> Criteria);

    private sealed record Response(
        [property: JsonPropertyName("answers")] Dictionary<string, Answer>? Answers,
        [property: JsonPropertyName("usage")] Usage? Usage);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")] long? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] long? CompletionTokens);

    private sealed record Answer(
        [property: JsonPropertyName("choice")] string? Choice,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("probabilities")] Dictionary<string, double>? Probabilities);

    public static void Configure(HttpClient client, AiOptions.OpenRouterSection openRouter, AiOptions.RoutingSection routing)
    {
        client.BaseAddress = new Uri(openRouter.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(routing.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouter.ApiKey);
    }
}

/// <summary>Stands in when the OpenRouter key or model is missing: every call fails, and the chat falls back to asking the user.</summary>
internal sealed class UnconfiguredRoutingChoice : IRoutingChoice
{
    public Task<RoutingChoice> ChooseAsync(
        string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct) =>
        throw new InvalidOperationException("AI:OpenRouter is not configured.");
}
