using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace BuildYourOwnAI.Api.Infrastructure.Ai;

public sealed class AiOptions
{
    public const int EmbeddingDimensions = 1536;

    public string ChatModel { get; set; } = "gpt-4.1-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public OpenAiSection OpenAI { get; set; } = new();

    public sealed class OpenAiSection
    {
        public string? ApiKey { get; set; }
    }
}

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Door 6: handlers only see <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>.
    /// Without a key the app still starts; every AI call then fails inside the handler and surfaces as 502.
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("AI").Get<AiOptions>() ?? new AiOptions();

        if (string.IsNullOrWhiteSpace(options.OpenAI.ApiKey))
        {
            services.AddSingleton<IChatClient, UnconfiguredAiClient>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, UnconfiguredAiClient>();
            return services;
        }

        var openAi = new OpenAIClient(new ApiKeyCredential(options.OpenAI.ApiKey));
        services.AddSingleton(openAi.GetChatClient(options.ChatModel).AsIChatClient());
        services.AddSingleton(openAi.GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator(AiOptions.EmbeddingDimensions));

        return services;
    }
}

/// <summary>Stands in for the provider when AI:OpenAI:ApiKey is missing: resolvable, but every call fails.</summary>
internal sealed class UnconfiguredAiClient : IChatClient, IEmbeddingGenerator<string, Embedding<float>>
{
    private static InvalidOperationException NotConfigured() => new("AI:OpenAI:ApiKey is not configured.");

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
