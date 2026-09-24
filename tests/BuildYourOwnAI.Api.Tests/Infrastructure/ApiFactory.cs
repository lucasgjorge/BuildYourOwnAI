using BuildYourOwnAI.Api.Infrastructure.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace BuildYourOwnAI.Api.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SpaMarker = "<!-- spa-index -->";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("byoai_tests")
        .WithUsername("byoai")
        .WithPassword("byoai")
        .Build();

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "byoai-webroot-" + Guid.NewGuid().ToString("N"));

    public FakeEmbeddingGenerator Embeddings { get; } = new();
    public FakeChatClient Chat { get; } = new();
    public FakeRouterClient Router { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "index.html"), $"<!doctype html><html><body>{SpaMarker}</body></html>");
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        try { Directory.Delete(_webRoot, recursive: true); } catch (IOException) { }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting(WebHostDefaults.WebRootKey, _webRoot);

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IChatClient>(Chat);
            services.RemoveAll<IRoutingChoice>();
            services.AddSingleton<IRoutingChoice>(Router);
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);
        });
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
