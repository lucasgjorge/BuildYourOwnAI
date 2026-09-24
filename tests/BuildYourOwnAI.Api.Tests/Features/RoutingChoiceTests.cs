using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class RoutingChoiceTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static string Marker() => "m" + Guid.NewGuid().ToString("N");

    private async Task<JsonElement> WithChoiceAsync(Func<IReadOnlyDictionary<string, string>, RoutingChoice> answer, Func<Task<HttpResponseMessage>> call)
    {
        Factory.Router.Override = answer;
        try
        {
            var response = await call();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await JsonAsync(response);
        }
        finally
        {
            Factory.Router.Override = null;
        }
    }

    // C1
    [Fact]
    public async Task Sends_question_and_one_option_per_assistant_plus_none()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        var hr = Marker();
        var culture = Marker();
        await CreateAssistantInAsync(client, nexora, "RH", routingDescription: hr);
        await CreateAssistantInAsync(client, nexora, "Culture", routingDescription: culture);
        IReadOnlyDictionary<string, string>? sent = null;
        var question = $"quando posso tirar ferias {Marker()}";

        await WithChoiceAsync(criteria =>
        {
            sent = criteria;
            return new RoutingChoice("1", 1, new Dictionary<string, double> { ["1"] = 1 });
        }, () => client.PostAsJsonAsync($"/api/organizations/{nexora}/route/ask", new { question }));

        Assert.Equal(["1", "2", "nenhuma"], sent!.Keys.Order());
        Assert.Contains("Culture", sent["1"]);
        Assert.Contains("Nexora", sent["1"]);
        Assert.Contains(culture, sent["1"]);
        Assert.Contains("RH", sent["2"]);
        Assert.Contains("Nexora", sent["2"]);
        Assert.Contains(hr, sent["2"]);
        Assert.StartsWith(question + "\n", Factory.Router.PromptContaining(question));
    }

    // C2
    [Fact]
    public async Task Confident_choice_answers_with_alternatives_by_probability()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var ids = new Dictionary<string, Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
            ids[name] = await CreateAssistantInAsync(client, organization, name, routingDescription: $"descricao {name}");

        var body = await WithChoiceAsync(
            _ => new RoutingChoice("2", 0.9, new Dictionary<string, double> { ["1"] = 0.02, ["2"] = 0.9, ["3"] = 0.03, ["4"] = 0.05, ["nenhuma"] = 0 }),
            () => client.PostAsJsonAsync($"/api/organizations/{organization}/route/ask", new { question = "pergunta" }));

        Assert.Equal("answered", body.GetProperty("kind").GetString());
        Assert.Equal(ids["B"], body.GetProperty("assistant").GetProperty("id").GetGuid());
        Assert.Equal([ids["D"], ids["C"], ids["A"]], body.GetProperty("alternatives").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
    }

    // C4
    [Theory]
    [InlineData(0.59, "clarify")]
    [InlineData(0.6, "answered")]
    public async Task Below_threshold_clarifies_by_probability(double confidence, string kind)
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var ids = new Dictionary<string, Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
            ids[name] = await CreateAssistantInAsync(client, organization, name, routingDescription: $"descricao {name}");
        var question = $"pergunta {Marker()}";

        var body = await WithChoiceAsync(
            _ => new RoutingChoice("3", confidence, new Dictionary<string, double> { ["1"] = 0.1, ["2"] = 0.3, ["3"] = confidence, ["4"] = 0.01 }),
            () => client.PostAsJsonAsync($"/api/organizations/{organization}/route/ask", new { question }));

        Assert.Equal(kind, body.GetProperty("kind").GetString());
        if (kind == "clarify")
        {
            Assert.Equal([ids["C"], ids["B"], ids["A"]], body.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()));
            Assert.False(Factory.Chat.ReceivedCallContaining(question));
        }
        else
        {
            Assert.Equal(ids["C"], body.GetProperty("assistant").GetProperty("id").GetGuid());
        }
    }

    // C8 (source-preview): the threshold is read from AI:Routing.
    [Theory]
    [InlineData(null, "answered")]
    [InlineData("0.95", "clarify")]
    public async Task Threshold_comes_from_routing_config(string? threshold, string kind)
    {
        using var app = threshold is null ? null : Factory.WithWebHostBuilder(b => b.UseSetting("AI:Routing:ConfidenceThreshold", threshold));
        var client = app is null
            ? await NewUserClientAsync()
            : app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        if (app is not null)
        {
            var email = NewEmail();
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();
        }
        var organization = await CreateOrganizationAsync(client);
        await CreateAssistantInAsync(client, organization, "RH", routingDescription: "processos de RH");

        var body = await WithChoiceAsync(
            _ => new RoutingChoice("1", 0.9, new Dictionary<string, double> { ["1"] = 0.9, ["nenhuma"] = 0.1 }),
            () => client.PostAsJsonAsync($"/api/organizations/{organization}/route/ask", new { question = "como peço férias?" }));

        Assert.Equal(kind, body.GetProperty("kind").GetString());
    }

    // C19 (source-preview): the timeout of the routing client comes from AI:Routing.
    [Theory]
    [InlineData(null, 10)]
    [InlineData("3", 3)]
    public void Timeout_comes_from_routing_config(string? seconds, int expected)
    {
        var settings = new Dictionary<string, string?> { ["AI:OpenRouter:ApiKey"] = "sk-or-test", ["AI:OpenRouter:Model"] = "choice-model-test" };
        if (seconds is not null) settings["AI:Routing:TimeoutSeconds"] = seconds;
        var services = new ServiceCollection();
        services.AddAi(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenRouterRoutingChoice));

        Assert.Equal(TimeSpan.FromSeconds(expected), client.Timeout);
        Assert.Equal("https://openrouter.ai/api/v1/", client.BaseAddress!.ToString());
    }
}

public sealed class OpenRouterRoutingChoiceTests
{
    private const string Key = "sk-or-test-key-3f9a";

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private static OpenRouterRoutingChoice Client(StubHandler handler)
    {
        var http = new HttpClient(handler);
        OpenRouterRoutingChoice.Configure(http,
            new AiOptions.OpenRouterSection { ApiKey = Key, Model = "choice-model-test" },
            new AiOptions.RoutingSection());
        return new OpenRouterRoutingChoice(http, "choice-model-test");
    }

    private static readonly Dictionary<string, string> Criteria = new() { ["1"] = "RH", ["2"] = "Culture", ["nenhuma"] = "outro assunto" };

    // C6
    [Fact]
    public async Task Posts_choice_request_and_reads_principal_answer()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"model":"provider/choice-model-1","answers":{"principal":{"type":"choice","choice":"1","confidence":0.97,
             "probabilities":{"1":0.97,"2":0.02,"nenhuma":0.01}}},"provider":"TypeSafe"}
            """);

        var choice = await Client(handler).ChooseAsync("quando posso tirar ferias", "Qual IA?", Criteria, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://openrouter.ai/api/v1/systemone", handler.Request.RequestUri!.ToString());
        Assert.Equal($"Bearer {Key}", handler.Request.Headers.Authorization!.ToString());
        using var sent = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("choice-model-test", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("quando posso tirar ferias", sent.RootElement.GetProperty("state").GetString());
        var question = sent.RootElement.GetProperty("questions").GetProperty("principal");
        Assert.Equal("choice", question.GetProperty("type").GetString());
        Assert.Equal("Qual IA?", question.GetProperty("instructions").GetString());
        Assert.Equal("Culture", question.GetProperty("criteria").GetProperty("2").GetString());
        Assert.Equal("1", choice.Choice);
        Assert.Equal(0.97, choice.Confidence);
        Assert.Equal(0.02, choice.Probabilities["2"]);
    }

    // C7
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"bad request body-marker-77c1"}}""")]
    [InlineData(HttpStatusCode.OK, """{"answers":{"outra":{"choice":"1","confidence":1}},"note":"body-marker-77c1"}""")]
    public async Task Failure_throws_without_body_or_key(HttpStatusCode status, string body)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            Client(new StubHandler(status, body)).ChooseAsync("pergunta", "Qual IA?", Criteria, CancellationToken.None));

        Assert.DoesNotContain("body-marker-77c1", exception.ToString());
        Assert.DoesNotContain(Key, exception.ToString());
    }
}
