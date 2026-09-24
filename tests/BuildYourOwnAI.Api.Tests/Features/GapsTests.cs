using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class GapsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static string Marker() => "m" + Guid.NewGuid().ToString("N");

    private static async Task<List<JsonElement>> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/gaps");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await JsonAsync(response)).EnumerateArray().ToList();
    }

    private static async Task<JsonElement> GapAsync(HttpClient client, string question) =>
        Assert.Single(await ListAsync(client), g => g.GetProperty("question").GetString() == question);

    /// <summary>Asks a question the fake chat answers with found=false; returns the question as stored and its gap id.</summary>
    private static async Task<(string Question, Guid GapId)> OpenGapAsync(HttpClient client, Guid assistant, string text)
    {
        var question = $"{text} {FakeAiTriggers.NotFound}";
        Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, assistant, question)).StatusCode);
        return (question, (await GapAsync(client, question)).GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid gap, string answer, Guid? organizationId = null) =>
        client.PostAsJsonAsync($"/api/gaps/{gap}/answer", new { answer, organizationId });

    private static Task<HttpResponseMessage> DismissAsync(HttpClient client, Guid gap) =>
        client.PostAsync($"/api/gaps/{gap}/dismiss", null);

    private Task<long> OpenGapCountAsync(string question) =>
        ScalarAsync("select count(*) from gaps where question = @q and status = 'open'", ("q", question));

    // C35
    [Fact]
    public async Task Unanswered_question_opens_gap()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME");
        var unanswered = $"qual o horario {Marker()} {FakeAiTriggers.NotFound}";
        var answered = $"qual o endereco {Marker()} {FakeAiTriggers.Found}";

        var notFound = await JsonAsync(await AskAsync(client, assistant, unanswered));
        var found = await JsonAsync(await AskAsync(client, assistant, answered));

        Assert.Equal(FakeAiTriggers.NotFoundAnswer, notFound.GetProperty("answer").GetString());
        Assert.False(notFound.GetProperty("found").GetBoolean());
        Assert.Equal(1, await ScalarAsync(
            "select count(*) from gaps where question = @q and status = 'open' and organization_id = @o and assistant_id = @a and ask_count = 1",
            ("q", unanswered), ("o", organization), ("a", assistant)));
        Assert.True(found.GetProperty("found").GetBoolean());
        Assert.Equal(0, await ScalarAsync("select count(*) from gaps where question = @q", ("q", answered)));
    }

    // C36
    [Fact]
    public async Task Jev_routed_unanswered_question_opens_gap()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var assistant = await CreateAssistantInAsync(client, organization, "Direto", routingDescription: "respostas curtas");
        var question = $"qual o horario {Marker()} {FakeAiTriggers.Route(1)} {FakeAiTriggers.NotFound}";

        var body = await JsonAsync(await JevAsync(client, question));

        Assert.Equal("answered", body.GetProperty("kind").GetString());
        Assert.False(body.GetProperty("found").GetBoolean());
        Assert.Equal(1, await ScalarAsync(
            "select count(*) from gaps where question = @q and status = 'open' and organization_id = @o and assistant_id = @a",
            ("q", question), ("o", organization), ("a", assistant)));
    }

    // C37
    [Fact]
    public async Task Same_normalized_question_increments_count()
    {
        var client = await NewUserClientAsync();
        var (_, acmeAssistant) = await NewAssistantAsync(client, "ACME");
        var (_, globexAssistant) = await NewAssistantAsync(client, "Globex", routingDescription: "respostas curtas");
        var seed = Marker();
        var question = $"Qual o horário? {seed} {FakeAiTriggers.NotFound}";

        await AskAsync(client, acmeAssistant, question);
        await AskAsync(client, acmeAssistant, $"  QUAL o   horário?   {seed}  {FakeAiTriggers.NotFound} ");
        await AskAsync(client, globexAssistant, question);
        var jevQuestion = $"previsao do tempo {Marker()} {FakeAiTriggers.Route("NONE")}";
        await JevAsync(client, jevQuestion);
        await JevAsync(client, jevQuestion);

        var gaps = await ListAsync(client);
        var byQuestion = gaps.Where(g => g.GetProperty("question").GetString() == question).ToList();
        Assert.Equal(2, byQuestion.Count);
        var acme = Assert.Single(byQuestion, g => g.GetProperty("organization").GetProperty("name").GetString() == "ACME");
        Assert.Equal(2, acme.GetProperty("askCount").GetInt32());
        Assert.True(acme.GetProperty("lastAskedAt").GetDateTimeOffset() > acme.GetProperty("firstAskedAt").GetDateTimeOffset());
        var globex = Assert.Single(byQuestion, g => g.GetProperty("organization").GetProperty("name").GetString() == "Globex");
        Assert.Equal(1, globex.GetProperty("askCount").GetInt32());
        var jev = await GapAsync(client, jevQuestion);
        Assert.Equal(2, jev.GetProperty("askCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, jev.GetProperty("organization").ValueKind);
    }

    // C38
    [Fact]
    public async Task Concurrent_same_question_yields_one_gap()
    {
        var client = await NewUserClientAsync();
        var assistant = await CreateAssistantAsync(client);
        // The rendezvous marker holds both requests inside the question embedding, so both reach the upsert together.
        var question = $"qual o horario {Marker()} {FakeAiTriggers.Rendezvous} {FakeAiTriggers.NotFound}";

        var results = await Task.WhenAll(AskAsync(client, assistant, question), AskAsync(client, assistant, question));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await OpenGapCountAsync(question));
        Assert.Equal(2, await ScalarAsync("select ask_count from gaps where question = @q", ("q", question)));
    }

    // C39
    [Fact]
    public async Task Unstructured_chat_reply_is_answered_without_gap()
    {
        var client = await NewUserClientAsync();
        var assistant = await CreateAssistantAsync(client);
        var question = $"qual o horario {Marker()}";

        var body = await JsonAsync(await AskAsync(client, assistant, question));

        Assert.Equal("fake answer", body.GetProperty("answer").GetString());
        Assert.True(body.GetProperty("found").GetBoolean());
        Assert.Equal(0, await ScalarAsync("select count(*) from gaps where question = @q", ("q", question)));
    }

    // C40
    [Fact]
    public async Task List_returns_own_open_gaps_most_asked_first()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME", routingDescription: "respostas curtas");
        var (once, _) = await OpenGapAsync(client, assistant, $"uma vez {Marker()}");
        var (thrice, _) = await OpenGapAsync(client, assistant, $"tres vezes {Marker()}");
        await AskAsync(client, assistant, thrice);
        await AskAsync(client, assistant, thrice);
        var (later, _) = await OpenGapAsync(client, assistant, $"depois {Marker()}");
        var (_, answered) = await OpenGapAsync(client, assistant, $"respondida {Marker()}");
        var (_, dismissed) = await OpenGapAsync(client, assistant, $"dispensada {Marker()}");
        Assert.Equal(HttpStatusCode.OK, (await AnswerAsync(client, answered, "resposta")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await DismissAsync(client, dismissed)).StatusCode);
        var jevQuestion = $"sem ia {Marker()} {FakeAiTriggers.Route("NONE")}";
        await JevAsync(client, jevQuestion);
        var other = await NewUserClientAsync();
        await OpenGapAsync(other, await CreateAssistantAsync(other), $"de outro {Marker()}");

        var gaps = await ListAsync(client);

        Assert.Equal([thrice, jevQuestion, later, once], gaps.Select(g => g.GetProperty("question").GetString()));
        var top = gaps[0];
        Assert.Equal(3, top.GetProperty("askCount").GetInt32());
        Assert.True(top.GetProperty("firstAskedAt").GetDateTimeOffset() < top.GetProperty("lastAskedAt").GetDateTimeOffset());
        Assert.Equal(organization, top.GetProperty("organization").GetProperty("id").GetGuid());
        Assert.Equal("ACME", top.GetProperty("organization").GetProperty("name").GetString());
        Assert.Equal(assistant, top.GetProperty("assistant").GetProperty("id").GetGuid());
        Assert.Equal("ACME", top.GetProperty("assistant").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, gaps[1].GetProperty("organization").ValueKind);
        Assert.Equal(JsonValueKind.Null, gaps[1].GetProperty("assistant").ValueKind);
        Assert.NotEqual(Guid.Empty, top.GetProperty("id").GetGuid());
    }

    // C41
    [Theory]
    [InlineData(1)]
    [InlineData(4000)]
    public async Task Answer_creates_document_and_closes_gap(int answerLength)
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME");
        var (question, gap) = await OpenGapAsync(client, assistant, $"qual e a politica de reembolso de despesas de viagem internacional {Marker()}");

        var response = await AnswerAsync(client, gap, new string('r', answerLength));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(gap, body.GetProperty("id").GetGuid());
        Assert.Equal("answered", body.GetProperty("status").GetString());
        var documentId = body.GetProperty("documentId").GetGuid();
        var documents = (await JsonAsync(await client.GetAsync($"/api/organizations/{organization}/documents"))).EnumerateArray();
        var document = Assert.Single(documents, d => d.GetProperty("id").GetGuid() == documentId);
        Assert.Equal($"Lacuna - {question[..60]}.md", document.GetProperty("fileName").GetString());
        Assert.DoesNotContain(await ListAsync(client), g => g.GetProperty("id").GetGuid() == gap);
    }

    // C42
    [Fact]
    public async Task Answered_gap_becomes_retrievable_knowledge()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var direct = await CreateAssistantInAsync(client, organization, "Direto");
        var teacher = await CreateAssistantInAsync(client, organization, "Professor");
        var (question, gap) = await OpenGapAsync(client, direct, $"qual o horario {Marker()}");
        var answer = $"das 9h as 18h {Marker()}";
        var documentId = (await JsonAsync(await AnswerAsync(client, gap, answer))).GetProperty("documentId").GetGuid();

        var body = await JsonAsync(await AskAsync(client, teacher, question));

        var source = Assert.Single(body.GetProperty("sources").EnumerateArray(), s => s.GetProperty("documentId").GetGuid() == documentId);
        Assert.Contains(answer, source.GetProperty("excerpt").GetString());
    }

    // C43
    [Fact]
    public async Task Gap_without_organization_requires_one()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        await CreateAssistantInAsync(client, organization, "Direto", routingDescription: "respostas curtas");
        var question = $"sem ia {Marker()} {FakeAiTriggers.Route("NONE")}";
        await JevAsync(client, question);
        var gap = (await GapAsync(client, question)).GetProperty("id").GetGuid();
        var other = await NewUserClientAsync();
        var foreign = await CreateOrganizationAsync(other);

        var missing = await AnswerAsync(client, gap, "resposta");
        var intruding = await AnswerAsync(client, gap, "resposta", foreign);
        var ok = await AnswerAsync(client, gap, $"resposta {Marker()}", organization);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.True((await JsonAsync(missing)).GetProperty("errors").TryGetProperty("organizationId", out _));
        Assert.Equal(HttpStatusCode.NotFound, intruding.StatusCode);
        Assert.Equal(0, await DocumentCountAsync(foreign));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var documentId = (await JsonAsync(ok)).GetProperty("documentId").GetGuid();
        Assert.Equal(1, await ScalarAsync("select count(*) from documents where id = @d and organization_id = @o", ("d", documentId), ("o", organization)));
    }

    // C44
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a4001")]
    public async Task Answer_bounds(string answer)
    {
        var client = await NewUserClientAsync();
        var (question, gap) = await OpenGapAsync(client, await CreateAssistantAsync(client), $"pergunta {Marker()}");
        if (answer == "a4001") answer = new string('a', 4001);

        var response = await AnswerAsync(client, gap, answer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("answer", out _));
        Assert.Equal(1, await OpenGapCountAsync(question));
    }

    // C45
    [Fact]
    public async Task Closed_gap_returns_409()
    {
        var client = await NewUserClientAsync();
        var assistant = await CreateAssistantAsync(client);
        var (_, answered) = await OpenGapAsync(client, assistant, $"respondida {Marker()}");
        var (_, dismissed) = await OpenGapAsync(client, assistant, $"dispensada {Marker()}");
        await AnswerAsync(client, answered, $"resposta {Marker()}");
        await DismissAsync(client, dismissed);

        foreach (var gap in new[] { answered, dismissed })
        {
            var answer = await AnswerAsync(client, gap, $"de novo {Marker()}");
            var dismiss = await DismissAsync(client, gap);
            Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
            Assert.Equal("application/problem+json", answer.Content.Headers.ContentType?.MediaType);
            Assert.Equal(HttpStatusCode.Conflict, dismiss.StatusCode);
            Assert.Equal("application/problem+json", dismiss.Content.Headers.ContentType?.MediaType);
        }
    }

    // C46
    [Fact]
    public async Task Embedding_failure_keeps_gap_open()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client);
        var (_, gap) = await OpenGapAsync(client, assistant, $"pergunta {Marker()}");

        var response = await AnswerAsync(client, gap, $"resposta {FakeAiTriggers.FailEmbedding}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
        Assert.Contains(await ListAsync(client), g => g.GetProperty("id").GetGuid() == gap);
        Assert.Equal(0, await DocumentCountAsync(organization));
    }

    // C47
    [Fact]
    public async Task Dismiss_closes_gap()
    {
        var client = await NewUserClientAsync();
        var (_, gap) = await OpenGapAsync(client, await CreateAssistantAsync(client), $"pergunta {Marker()}");

        var response = await DismissAsync(client, gap);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await ListAsync(client), g => g.GetProperty("id").GetGuid() == gap);
        Assert.Equal(1, await ScalarAsync("select count(*) from gaps where id = @id and status = 'dismissed'", ("id", gap)));
    }

    // C48
    [Theory]
    [InlineData("answer", false)]
    [InlineData("answer", true)]
    [InlineData("dismiss", false)]
    [InlineData("dismiss", true)]
    public async Task Foreign_or_missing_gap_returns_404(string action, bool missing)
    {
        var owner = await NewUserClientAsync();
        var (question, foreignGap) = await OpenGapAsync(owner, await CreateAssistantAsync(owner), $"do dono {Marker()}");
        var intruder = await NewUserClientAsync();
        var target = missing ? Guid.NewGuid() : foreignGap;

        var response = action == "answer" ? await AnswerAsync(intruder, target, "resposta") : await DismissAsync(intruder, target);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, await OpenGapCountAsync(question));
    }

    // C49
    [Fact]
    public async Task Deleting_assistant_keeps_its_gaps()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME");
        var (question, _) = await OpenGapAsync(client, assistant, $"pergunta {Marker()}");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/assistants/{assistant}")).StatusCode);

        var gap = await GapAsync(client, question);
        Assert.Equal(JsonValueKind.Null, gap.GetProperty("assistant").ValueKind);
        Assert.Equal(organization, gap.GetProperty("organization").GetProperty("id").GetGuid());
    }
}
