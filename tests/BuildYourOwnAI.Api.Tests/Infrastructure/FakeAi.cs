using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Tests.Infrastructure;

public static class FakeAiTriggers
{
    // Any text containing these markers makes the corresponding fake throw.
    public const string FailEmbedding = "__FAIL_EMBED__";
    public const string FailChat = "__FAIL_CHAT__";
    public const string ProviderSecretMessage = "provider-secret-message-7f3a";
    // Two embedding calls containing this marker wait for each other before returning (forces a race).
    public const string Rendezvous = "__RENDEZVOUS__";

    // The answering chat replies with the structured {answer, found} JSON when the prompt contains one of these.
    public const string NotFound = "__NOT_FOUND__";
    public const string Found = "__FOUND__";
    public const string NotFoundAnswer = "não sei";
    public const string FoundAnswer = "resposta-9c1e";

    /// <summary>Router behaviour, put in the question: __ROUTE_2__, __ROUTE_LOW2__, __ROUTE_NONE__, __ROUTE_NONELOW__, __ROUTE_THROW__, __ROUTE_TEXT__.</summary>
    public static string Route(object behaviour) => $"__ROUTE_{behaviour}__";
}

/// <summary>Deterministic bag-of-words embedding: same text, same vector; shared words, smaller cosine distance.</summary>
public sealed partial class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 1536;

    private readonly Lock _rendezvousLock = new();
    private TaskCompletionSource? _rendezvousWaiter;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = values.ToList();
        if (list.Any(v => v.Contains(FakeAiTriggers.FailEmbedding)))
            throw new HttpRequestException(FakeAiTriggers.ProviderSecretMessage);
        if (list.Any(v => v.Contains(FakeAiTriggers.Rendezvous)))
            await RendezvousAsync();

        return new GeneratedEmbeddings<Embedding<float>>(list.Select(v => new Embedding<float>(Embed(v))));
    }

    private Task RendezvousAsync()
    {
        lock (_rendezvousLock)
        {
            if (_rendezvousWaiter is { } first)
            {
                _rendezvousWaiter = null;
                first.SetResult();
                return Task.CompletedTask;
            }
            _rendezvousWaiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _rendezvousWaiter.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (Match m in Word().Matches(text.ToLowerInvariant()))
            vector[Bucket(m.Value)] += 1f;

        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm == 0) { vector[0] = 1f; return vector; }
        for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    public static double CosineDistance(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return 1 - dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static int Bucket(string word)
    {
        uint hash = 2166136261;
        foreach (var c in word) { hash ^= c; hash *= 16777619; }
        return (int)(hash % Dimensions);
    }

    [GeneratedRegex(@"[\p{L}\p{N}_]+")]
    private static partial Regex Word();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

public sealed class FakeChatClient : IChatClient
{
    public ConcurrentQueue<IReadOnlyList<ChatMessage>> Calls { get; } = new();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Calls.Enqueue(list);
        if (list.Any(m => m.Text.Contains(FakeAiTriggers.FailChat)))
            throw new HttpRequestException(FakeAiTriggers.ProviderSecretMessage);

        var text = string.Join("\n", list.Select(m => m.Text));
        var reply = text.Contains(FakeAiTriggers.NotFound)
            ? JsonSerializer.Serialize(new { answer = FakeAiTriggers.NotFoundAnswer, found = false })
            : text.Contains(FakeAiTriggers.Found)
                ? JsonSerializer.Serialize(new { answer = FakeAiTriggers.FoundAnswer, found = true })
                : "fake answer";
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public bool ReceivedCallContaining(string marker) => Calls.Any(c => c.Any(m => m.Text.Contains(marker)));

    /// <summary>All text the model received in the call whose messages contain <paramref name="marker"/>.</summary>
    public string PromptContaining(string marker) =>
        Calls.Select(c => string.Join("\n", c.Select(m => m.Text))).Single(t => t.Contains(marker));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>
/// Stands in for the routing "choice" primitive; decides by the __ROUTE_x__ marker in the question (the state).
/// A number picks that option with confidence 1, LOWn picks it with 0.3, NONE picks "nenhuma", TEXT picks a key
/// that was never offered, THROW fails. The chosen option gets the probability of its confidence, the rest share what is left, each below the chosen one.
/// </summary>
public sealed partial class FakeRouterClient : IRoutingChoice
{
    public const string OutputMarker = "router-output-5d1c";

    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>When set, answers every call instead of the markers (reset it in a finally).</summary>
    public Func<IReadOnlyDictionary<string, string>, RoutingChoice>? Override { get; set; }

    public Task<RoutingChoice> ChooseAsync(
        string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct)
    {
        Calls.Enqueue(string.Join("\n", [state, instructions, .. criteria.Select(c => $"{c.Key}: {c.Value}")]));
        if (Override is { } answer)
            return Task.FromResult(answer(criteria));

        var behaviour = Marker().Match(state) is { Success: true } m ? m.Groups[1].Value : "1";
        return Task.FromResult(behaviour switch
        {
            "THROW" => throw new HttpRequestException(FakeAiTriggers.ProviderSecretMessage),
            "TEXT" => new RoutingChoice(OutputMarker, 1, new Dictionary<string, double>()),
            "NONE" => Pick(criteria, "nenhuma", 0.9),
            "NONELOW" => Pick(criteria, "nenhuma", 0.3),
            _ when behaviour.StartsWith("LOW") => Pick(criteria, behaviour[3..], 0.3),
            _ => Pick(criteria, behaviour, 1),
        });
    }

    private static RoutingChoice Pick(IReadOnlyDictionary<string, string> criteria, string key, double confidence)
    {
        var others = criteria.Keys.Where(k => k != key).ToList();
        // The chosen option is always the most likely one, as it is for the real primitive.
        var share = others.Count == 0 ? 0 : Math.Min((1 - confidence) / others.Count, confidence / 2);
        var probabilities = others.ToDictionary(k => k, _ => share);
        probabilities[key] = confidence;
        return new RoutingChoice(key, confidence, probabilities);
    }

    public string PromptContaining(string marker) => Calls.Single(c => c.Contains(marker));

    public bool ReceivedCallContaining(string marker) => Calls.Any(c => c.Contains(marker));

    [GeneratedRegex(@"__ROUTE_([A-Z]*\d*)__")]
    private static partial Regex Marker();
}
