using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class OrganizationRoutingTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static string Marker() => "m" + Guid.NewGuid().ToString("N");

    private static Task<HttpResponseMessage> OrgRouteAsync(HttpClient client, Guid organization, string question) =>
        client.PostAsJsonAsync($"/api/organizations/{organization}/route/ask", new { question });

    // C1
    [Fact]
    public async Task Router_sees_only_this_organization()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        var acme = await CreateOrganizationAsync(client, "ACME");
        var hr = Marker();
        var otherOrg = Marker();
        var foreign = Marker();
        await CreateAssistantInAsync(client, nexora, "RH", routingDescription: hr);
        await CreateAssistantInAsync(client, nexora, "SemDescricao");
        await CreateAssistantInAsync(client, acme, "Vendas", routingDescription: otherOrg);
        var other = await NewUserClientAsync();
        await CreateAssistantInAsync(other, await CreateOrganizationAsync(other), "Alheia", routingDescription: foreign);
        var question = $"como peço ferias {Marker()}";

        Assert.Equal(HttpStatusCode.OK, (await OrgRouteAsync(client, nexora, question)).StatusCode);

        var prompt = Factory.Router.PromptContaining(question);
        Assert.Contains("RH", prompt);
        Assert.Contains(hr, prompt);
        Assert.DoesNotContain("SemDescricao", prompt);
        Assert.DoesNotContain("Vendas", prompt);
        Assert.DoesNotContain(otherOrg, prompt);
        Assert.DoesNotContain(foreign, prompt);
    }

    // C2
    [Fact]
    public async Task Confident_choice_answers_within_organization()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        var acme = await CreateOrganizationAsync(client, "ACME");
        // Offered by name: 1 = Culture, 2 = RH.
        var culture = await CreateAssistantInAsync(client, nexora, "Culture", routingDescription: "cultura da empresa");
        var hr = await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");
        await CreateAssistantInAsync(client, acme, "Admin", routingDescription: "qualquer coisa");
        var document = await UploadOkAsync(client, nexora, "rh.txt", "ferias sao pedidas pelo portal");
        await UploadOkAsync(client, acme, "acme.txt", "ferias na acme sao outra coisa");

        var response = await OrgRouteAsync(client, nexora, $"ferias {FakeAiTriggers.Route(2)} {FakeAiTriggers.Found}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("answered", body.GetProperty("kind").GetString());
        Assert.Equal(hr, body.GetProperty("assistant").GetProperty("id").GetGuid());
        Assert.Equal("Nexora", body.GetProperty("assistant").GetProperty("organizationName").GetString());
        Assert.Equal(FakeAiTriggers.FoundAnswer, body.GetProperty("answer").GetString());
        Assert.True(body.GetProperty("found").GetBoolean());
        Assert.Equal([document], body.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("documentId").GetGuid()));
        Assert.Equal([culture], body.GetProperty("alternatives").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
    }

    // C3
    [Fact]
    public async Task No_match_records_gap_in_organization()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");
        var question = $"previsao do tempo {Marker()} {FakeAiTriggers.Route("NONE")}";

        var response = await OrgRouteAsync(client, nexora, question);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noMatch", (await JsonAsync(response)).GetProperty("kind").GetString());
        Assert.Equal(1, await ScalarAsync(
            "select count(*) from gaps where question = @q and status = 'open' and organization_id = @o and assistant_id is null",
            ("q", question), ("o", nexora)));
    }

    // C4
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_or_missing_organization_returns_404(bool missing)
    {
        var owner = await NewUserClientAsync();
        var foreign = await CreateOrganizationAsync(owner);
        await CreateAssistantInAsync(owner, foreign, "RH", routingDescription: "processos de RH");
        var intruder = await NewUserClientAsync();
        await CreateAssistantInAsync(intruder, await CreateOrganizationAsync(intruder), "Minha", routingDescription: "tudo");
        var question = $"pergunta {Marker()}";

        var response = await OrgRouteAsync(intruder, missing ? Guid.NewGuid() : foreign, question);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(Factory.Router.ReceivedCallContaining(question));
    }

    // C5
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_eligible_in_this_organization_returns_422(bool withUndescribedAssistant)
    {
        var client = await NewUserClientAsync();
        var empty = await CreateOrganizationAsync(client, "Vazia");
        if (withUndescribedAssistant)
            await CreateAssistantInAsync(client, empty, "SemDescricao");
        await CreateAssistantInAsync(client, await CreateOrganizationAsync(client, "Outra"), "RH", routingDescription: "processos de RH");
        var question = $"pergunta {Marker()}";

        var response = await OrgRouteAsync(client, empty, question);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(Factory.Router.ReceivedCallContaining(question));
    }

    // C6
    [Theory]
    [InlineData("empty")]
    [InlineData("too-long")]
    [InlineData("low-confidence")]
    [InlineData("router-throws")]
    [InlineData("chat-fails")]
    public async Task Keeps_all_assistants_routing_rules(string scenario)
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");
        await CreateAssistantInAsync(client, nexora, "Culture", routingDescription: "cultura da empresa");
        var question = scenario switch
        {
            "empty" => "   ",
            "too-long" => new string('a', 2001),
            "low-confidence" => $"pergunta {FakeAiTriggers.Route("LOW1")}",
            "router-throws" => $"pergunta {FakeAiTriggers.Route("THROW")}",
            _ => $"pergunta {FakeAiTriggers.Route(1)} {FakeAiTriggers.FailChat}",
        };

        var response = await OrgRouteAsync(client, nexora, question);

        switch (scenario)
        {
            case "empty" or "too-long":
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("question", out _));
                break;
            case "low-confidence" or "router-throws":
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("clarify", (await JsonAsync(response)).GetProperty("kind").GetString());
                break;
            default:
                Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
                Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
                break;
        }
    }

    [Fact]
    public async Task Follow_up_is_routed_with_the_previous_turn()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        // Offered by name: 1 = Culture, 2 = RH.
        await CreateAssistantInAsync(client, nexora, "Culture", routingDescription: "cultura da empresa");
        var hr = await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");
        var stranger = await NewUserClientAsync();
        var foreign = await CreateAssistantInAsync(stranger, await CreateOrganizationAsync(stranger), "Alheia", routingDescription: "x");
        await UploadOkAsync(client, nexora, "rh.txt", "ferias sao pedidas pelo portal com trinta dias");
        var previous = $"como peço ferias {Marker()}";
        var question = $"e quantos dias {Marker()} {FakeAiTriggers.Route(2)}";

        var response = await client.PostAsJsonAsync($"/api/organizations/{nexora}/route/ask",
            new { question, previous = new { question = previous, assistantId = hr } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(hr, (await JsonAsync(response)).GetProperty("assistant").GetProperty("id").GetGuid());
        var routed = Factory.Router.PromptContaining(question);
        Assert.Contains($"Pergunta anterior (respondida por RH): {previous}", routed);
        Assert.Contains($"Pergunta atual: {question}", routed);
        Assert.Contains(previous, Factory.Chat.PromptContaining(question));
        // Only the current question can become a gap; the previous one is context.
        Assert.Equal(0, await ScalarAsync("select count(*) from gaps where question like @q", ("q", $"%{previous}%")));

        // An assistant id the user does not own names nobody.
        var other = $"e agora {Marker()}";
        await client.PostAsJsonAsync($"/api/organizations/{nexora}/route/ask",
            new { question = other, previous = new { question = previous, assistantId = foreign } });
        Assert.DoesNotContain("Alheia", Factory.Router.PromptContaining(other));
        Assert.Contains($"Pergunta anterior: {previous}", Factory.Router.PromptContaining(other));
    }

    [Fact]
    public async Task Too_long_previous_question_is_rejected()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");

        var response = await client.PostAsJsonAsync($"/api/organizations/{nexora}/route/ask",
            new { question = "e quantos dias", previous = new { question = new string('a', 2001), assistantId = (Guid?)null } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("previous.question", out _));
    }

    // C7
    [Fact]
    public async Task Shares_rate_limit_with_ask_and_all_assistants_routing()
    {
        var client = await NewUserClientAsync();
        var nexora = await CreateOrganizationAsync(client, "Nexora");
        var hr = await CreateAssistantInAsync(client, nexora, "RH", routingDescription: "processos de RH");

        for (var i = 1; i <= 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, hr, $"pergunta {i}")).StatusCode);
        for (var i = 11; i <= 15; i++)
            Assert.Equal(HttpStatusCode.OK, (await RouteAsync(client, $"pergunta {i}")).StatusCode);
        for (var i = 16; i <= 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await OrgRouteAsync(client, nexora, $"pergunta {i}")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await OrgRouteAsync(client, nexora, "pergunta 21")).StatusCode);
    }
}
