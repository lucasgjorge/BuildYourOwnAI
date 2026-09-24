using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class JevTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static string Marker() => "m" + Guid.NewGuid().ToString("N");

    private static List<Guid> Ids(JsonElement array) => array.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();

    // C18
    [Fact]
    public async Task Router_sees_only_own_assistants_with_description()
    {
        var client = await NewUserClientAsync();
        var acme = await CreateOrganizationAsync(client, "ACME");
        var globex = await CreateOrganizationAsync(client, "Globex");
        var directDescription = Marker();
        var teacherDescription = Marker();
        await CreateAssistantInAsync(client, acme, "Direto", routingDescription: directDescription);
        await CreateAssistantInAsync(client, globex, "Professor", routingDescription: teacherDescription);
        await CreateAssistantInAsync(client, acme, "SemDescricao");
        await CreateAssistantInAsync(client, acme, "SoEspacos", routingDescription: "   ");
        var other = await NewUserClientAsync();
        var foreignDescription = Marker();
        await CreateAssistantInAsync(other, await CreateOrganizationAsync(other, "Alheia"), "Alheia", routingDescription: foreignDescription);
        var question = $"qual o horario {Marker()}";

        Assert.Equal(HttpStatusCode.OK, (await JevAsync(client, question)).StatusCode);

        var prompt = Factory.Router.PromptContaining(question);
        Assert.Contains("Direto", prompt);
        Assert.Contains("ACME", prompt);
        Assert.Contains(directDescription, prompt);
        Assert.Contains("Professor", prompt);
        Assert.Contains("Globex", prompt);
        Assert.Contains(teacherDescription, prompt);
        Assert.DoesNotContain("SemDescricao", prompt);
        Assert.DoesNotContain("SoEspacos", prompt);
        Assert.DoesNotContain(foreignDescription, prompt);
    }

    // C19
    [Fact]
    public async Task Confident_choice_answers_through_chosen_assistant()
    {
        var client = await NewUserClientAsync();
        var acme = await CreateOrganizationAsync(client, "ACME");
        var globex = await CreateOrganizationAsync(client, "Globex");
        // Offered to the router ordered by name: 1 = Direto, 2 = Professor.
        var direct = await CreateAssistantInAsync(client, acme, "Direto", routingDescription: "respostas curtas");
        var teacher = await CreateAssistantInAsync(client, globex, "Professor", routingDescription: "quando a pessoa quer entender");
        var globexDoc = await UploadOkAsync(client, globex, "aula.txt", "fotossintese acontece nas folhas");
        await UploadOkAsync(client, acme, "acme.txt", "manual da acme sobre fotossintese");

        var response = await JevAsync(client, $"me explica fotossintese {FakeAiTriggers.Route(2)} {FakeAiTriggers.Found}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("answered", body.GetProperty("kind").GetString());
        var assistant = body.GetProperty("assistant");
        Assert.Equal(teacher, assistant.GetProperty("id").GetGuid());
        Assert.Equal("Professor", assistant.GetProperty("name").GetString());
        Assert.Equal("Globex", assistant.GetProperty("organizationName").GetString());
        Assert.Equal(FakeAiTriggers.FoundAnswer, body.GetProperty("answer").GetString());
        Assert.True(body.GetProperty("found").GetBoolean());
        Assert.Equal([globexDoc], body.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("documentId").GetGuid()));
        var alternatives = body.GetProperty("alternatives");
        Assert.Equal([direct], Ids(alternatives));
        Assert.Equal("Direto", alternatives[0].GetProperty("name").GetString());
        Assert.Equal("ACME", alternatives[0].GetProperty("organizationName").GetString());
    }

    // C20
    [Fact]
    public async Task Alternatives_are_capped_at_three()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
            await CreateAssistantInAsync(client, organization, name, routingDescription: $"descricao {name}");

        var body = await JsonAsync(await JevAsync(client, $"pergunta {FakeAiTriggers.Route(1)}"));

        Assert.Equal("answered", body.GetProperty("kind").GetString());
        Assert.Equal("A", body.GetProperty("assistant").GetProperty("name").GetString());
        Assert.Equal(3, body.GetProperty("alternatives").GetArrayLength());
        Assert.DoesNotContain("A", body.GetProperty("alternatives").EnumerateArray().Select(a => a.GetProperty("name").GetString()));
    }

    // C21
    [Fact]
    public async Task Low_confidence_returns_clarify_without_answering()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var direct = await CreateAssistantInAsync(client, organization, "Direto", routingDescription: "respostas curtas");
        var teacher = await CreateAssistantInAsync(client, organization, "Professor", routingDescription: "explicacoes");
        var question = $"ferias {Marker()} {FakeAiTriggers.Route("LOW1")}";

        var response = await JevAsync(client, question);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("clarify", body.GetProperty("kind").GetString());
        var candidates = Ids(body.GetProperty("candidates"));
        Assert.InRange(candidates.Count, 1, 3);
        Assert.Equal(direct, candidates[0]);
        Assert.Contains(teacher, candidates);
        Assert.False(body.TryGetProperty("answer", out _));
        Assert.False(Factory.Chat.ReceivedCallContaining(question));
    }

    // C22
    [Fact]
    public async Task No_match_records_gap_without_organization()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        await CreateAssistantInAsync(client, organization, "Direto", routingDescription: "respostas curtas");
        var question = $"previsao do tempo {Marker()} {FakeAiTriggers.Route("NONE")}";

        var response = await JevAsync(client, question);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noMatch", (await JsonAsync(response)).GetProperty("kind").GetString());
        Assert.Equal(1, await ScalarAsync(
            "select count(*) from gaps where question = @q and status = 'open' and organization_id is null and assistant_id is null",
            ("q", question)));
    }

    // C23
    [Theory]
    [InlineData("THROW")]
    [InlineData("TEXT")]
    [InlineData("0")]
    [InlineData("7")]
    public async Task Router_failure_falls_back_to_clarify(string behaviour)
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        // Six eligible (N = 6, so 7 is out of range), created out of name order.
        foreach (var name in new[] { "F", "B", "E", "A", "D", "C" })
            await CreateAssistantInAsync(client, organization, name, routingDescription: $"descricao {name}");

        var response = await JevAsync(client, $"pergunta {FakeAiTriggers.Route(behaviour)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("clarify", body.GetProperty("kind").GetString());
        Assert.Equal(["A", "B", "C", "D", "E"], body.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
    }

    // C24
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_eligible_assistant_returns_422(bool withUndescribedAssistant)
    {
        var client = await NewUserClientAsync();
        if (withUndescribedAssistant)
            await CreateAssistantInAsync(client, await CreateOrganizationAsync(client), "SemDescricao");
        var question = $"pergunta {Marker()}";

        var response = await JevAsync(client, question);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(Factory.Router.ReceivedCallContaining(question));
    }

    // C25
    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("   ", HttpStatusCode.BadRequest)]
    [InlineData("q2001", HttpStatusCode.BadRequest)]
    [InlineData("q2000", HttpStatusCode.OK)]
    public async Task Question_bounds(string question, HttpStatusCode expected)
    {
        var client = await NewUserClientAsync();
        await CreateAssistantInAsync(client, await CreateOrganizationAsync(client), "Direto", routingDescription: "respostas curtas");
        if (question.StartsWith("q2")) question = new string('a', int.Parse(question[1..]));

        var response = await JevAsync(client, question);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
            Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("question", out _));
    }

    // C26
    [Fact]
    public async Task Shares_rate_limit_with_ask()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantInAsync(client, await CreateOrganizationAsync(client), "Direto", routingDescription: "respostas curtas");

        for (var i = 1; i <= 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, id, $"pergunta {i}")).StatusCode);
        for (var i = 11; i <= 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await JevAsync(client, $"pergunta {i}")).StatusCode);

        var limited = await JevAsync(client, "pergunta 21");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    // C27
    [Fact]
    public async Task Router_never_receives_instructions_or_documents()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var instructionsMarker = Marker();
        var documentMarker = Marker();
        await CreateAssistantInAsync(client, organization, "Direto", $"instrucoes {instructionsMarker}", "respostas curtas");
        await UploadOkAsync(client, organization, "a.txt", $"politica de ferias {documentMarker}");
        var question = $"politica de ferias {Marker()}";

        await JevAsync(client, question);

        var prompt = Factory.Router.PromptContaining(question);
        Assert.DoesNotContain(instructionsMarker, prompt);
        Assert.DoesNotContain(documentMarker, prompt);
    }

    // C28
    [Fact]
    public async Task Answer_failure_after_routing_returns_502()
    {
        var client = await NewUserClientAsync();
        await CreateAssistantInAsync(client, await CreateOrganizationAsync(client), "Direto", routingDescription: "respostas curtas");

        var response = await JevAsync(client, $"pergunta {FakeAiTriggers.Route(1)} {FakeAiTriggers.FailChat}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
    }
}
