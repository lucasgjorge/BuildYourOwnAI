using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BuildYourOwnAI.Api.Infrastructure.Ai;

/// <summary>What Jev picked among the options it was given, how sure it is, and the distribution over all options.</summary>
public sealed record JevChoice(string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities);

/// <summary>
/// Door 1 (jev-choice, AD-012): the "choice" primitive behind Jev. Handlers see only this; the HTTP contract of
/// the provider stays in <see cref="OpenRouterJevChoice"/>.
/// </summary>
public interface IJevChoice
{
    Task<JevChoice> ChooseAsync(string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct);
}

/// <summary>
/// TypeSafe "System One" via OpenRouter: <c>POST {base}systemone</c>, not the chat completions endpoint.
/// The model is the bare name (e.g. <c>jev-latest</c>).
/// </summary>
public sealed class OpenRouterJevChoice(HttpClient http, string model) : IJevChoice
{
    private const string QuestionKey = "principal";

    public async Task<JevChoice> ChooseAsync(
        string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct)
    {
        var request = new Request(model, state, new Dictionary<string, Question>
        {
            [QuestionKey] = new("choice", instructions, criteria),
        });

        using var response = await http.PostAsJsonAsync("systemone", request, ct);
        // The provider's body may echo the question; it never goes into the exception.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Jev returned {(int)response.StatusCode}.", null, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response>(ct);
        if (body?.Answers is null || !body.Answers.TryGetValue(QuestionKey, out var answer) || answer.Choice is null)
            throw new InvalidOperationException("Jev response has no answer for the question.");

        return new JevChoice(answer.Choice, answer.Confidence, answer.Probabilities ?? new Dictionary<string, double>());
    }

    private sealed record Request(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("questions")] IReadOnlyDictionary<string, Question> Questions);

    private sealed record Question(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("instructions")] string Instructions,
        [property: JsonPropertyName("criteria")] IReadOnlyDictionary<string, string> Criteria);

    private sealed record Response([property: JsonPropertyName("answers")] Dictionary<string, Answer>? Answers);

    private sealed record Answer(
        [property: JsonPropertyName("choice")] string? Choice,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("probabilities")] Dictionary<string, double>? Probabilities);

    public static void Configure(HttpClient client, AiOptions.OpenRouterSection openRouter, AiOptions.JevSection jev)
    {
        client.BaseAddress = new Uri(openRouter.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(jev.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouter.ApiKey);
    }
}

/// <summary>Stands in when the OpenRouter key or model is missing: every call fails, and Jev falls back to asking the user.</summary>
internal sealed class UnconfiguredJevChoice : IJevChoice
{
    public Task<JevChoice> ChooseAsync(
        string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct) =>
        throw new InvalidOperationException("AI:OpenRouter is not configured.");
}
