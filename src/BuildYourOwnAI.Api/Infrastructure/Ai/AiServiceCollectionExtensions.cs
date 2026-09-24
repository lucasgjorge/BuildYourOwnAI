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
    /// The OpenAI client is built lazily so the app starts without a key; calls then fail and surface as 502.
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("AI").Get<AiOptions>() ?? new AiOptions();

        services.AddSingleton(_ => new OpenAIClient(new ApiKeyCredential(RequireKey(options))));
        services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<OpenAIClient>().GetChatClient(options.ChatModel).AsIChatClient());
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            sp.GetRequiredService<OpenAIClient>().GetEmbeddingClient(options.EmbeddingModel)
                .AsIEmbeddingGenerator(AiOptions.EmbeddingDimensions));

        return services;
    }

    private static string RequireKey(AiOptions options) =>
        string.IsNullOrWhiteSpace(options.OpenAI.ApiKey)
            ? throw new InvalidOperationException("AI:OpenAI:ApiKey is not configured.")
            : options.OpenAI.ApiKey;
}
