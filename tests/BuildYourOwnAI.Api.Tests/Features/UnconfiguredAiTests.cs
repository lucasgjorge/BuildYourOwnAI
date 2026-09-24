using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildYourOwnAI.Api.Tests.Features;

/// <summary>Found by the smoke run: with no OpenAI key the app must still answer provider failures with 502, not 500.</summary>
public sealed class UnconfiguredAiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Missing_openai_key_yields_502_on_upload_and_ask()
    {
        using var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddAi(new ConfigurationBuilder().Build());
        }));
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();
        var (org, id) = await NewAssistantAsync(client);

        var upload = await UploadTextAsync(client, org, "a.txt", $"conteudo {Guid.NewGuid()}");
        var ask = await AskAsync(client, id, "oi");

        Assert.Equal(HttpStatusCode.BadGateway, upload.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, ask.StatusCode);
    }

    // C29
    [Fact]
    public async Task Routing_without_key_falls_back_to_clarify()
    {
        using var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAi(new ConfigurationBuilder().Build())));
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();
        var assistant = await CreateAssistantInAsync(client, await CreateOrganizationAsync(client), "Direto", routingDescription: "respostas curtas");

        var response = await RouteAsync(client, "qual o horario?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("clarify", body.GetProperty("kind").GetString());
        Assert.Equal(assistant, body.GetProperty("candidates")[0].GetProperty("id").GetGuid());
    }
}
