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
    public OpenRouterSection OpenRouter { get; set; } = new();
    public RoutingSection Routing { get; set; } = new();

    public sealed class OpenAiSection
    {
        public string? ApiKey { get; set; }
    }

    public sealed class OpenRouterSection
    {
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public string BaseAddress { get; set; } = "https://openrouter.ai/api/v1/";
    }

    public sealed class RoutingSection
    {
        /// <summary>At or above this confidence the automatic choice is followed; below it, the user picks (AD-012).</summary>
        public double ConfidenceThreshold { get; set; } = 0.6;
        public int TimeoutSeconds { get; set; } = 10;
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
        services.Configure<AiOptions>(configuration.GetSection("AI"));

        // AD-012: the automatic choice is the "choice" primitive on OpenRouter. Without key and model, the chat falls back to asking the user.
        if (string.IsNullOrWhiteSpace(options.OpenRouter.ApiKey) || string.IsNullOrWhiteSpace(options.OpenRouter.Model))
        {
            services.AddSingleton<IRoutingChoice, UnconfiguredRoutingChoice>();
        }
        else
        {
            var model = options.OpenRouter.Model;
            services.AddHttpClient(nameof(OpenRouterRoutingChoice), client => OpenRouterRoutingChoice.Configure(client, options.OpenRouter, options.Routing));
            services.AddTransient<IRoutingChoice>(sp =>
                new OpenRouterRoutingChoice(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenRouterRoutingChoice)), model));
        }

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

/// <summary>Stands in for a provider whose key is missing: resolvable, but every call fails.</summary>
internal sealed class UnconfiguredAiClient : IChatClient, IEmbeddingGenerator<string, Embedding<float>>
{
    private static InvalidOperationException NotConfigured() => new("The AI provider key is not configured.");

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
