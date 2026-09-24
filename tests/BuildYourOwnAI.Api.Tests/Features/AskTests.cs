using System.Net;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class AskTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static string Marker() => "m" + Guid.NewGuid().ToString("N");

    // doc k: k occurrences of "alpha" and (8 - k) of a word unique to it -> cosine to "alpha" grows strictly with k.
    private static string GradedDoc(int k) =>
        string.Join(' ', Enumerable.Repeat("alpha", k).Concat(Enumerable.Repeat($"filler{k}x", 8 - k)));

    // C22
    [Fact]
    public async Task Ask_returns_answer_with_top5_sources()
    {
        var client = await NewUserClientAsync();
        var (org, id) = await NewAssistantAsync(client);
        var docs = new Dictionary<Guid, int>();
        for (var k = 1; k <= 7; k++)
            docs[await UploadOkAsync(client, org, $"doc{k}.txt", GradedDoc(k))] = k;

        var response = await AskAsync(client, id, "alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("answer").GetString()));
        var sources = body.GetProperty("sources").EnumerateArray().ToList();
        Assert.Equal([7, 6, 5, 4, 3], sources.Select(s => docs[s.GetProperty("documentId").GetGuid()]));
        foreach (var s in sources)
        {
            var k = docs[s.GetProperty("documentId").GetGuid()];
            Assert.Equal($"doc{k}.txt", s.GetProperty("fileName").GetString());
            Assert.Equal(0, s.GetProperty("chunkIndex").GetInt32());
            Assert.Equal(GradedDoc(k), s.GetProperty("excerpt").GetString());
        }

        var (smallOrg, small) = await NewAssistantAsync(client, "Pequena");
        await UploadOkAsync(client, smallOrg, "um.txt", GradedDoc(2));
        await UploadOkAsync(client, smallOrg, "dois.txt", GradedDoc(5));
        var smallSources = (await JsonAsync(await AskAsync(client, small, "alpha"))).GetProperty("sources");
        Assert.Equal(2, smallSources.GetArrayLength());
    }

    // C23
    [Fact]
    public async Task Ask_never_retrieves_from_another_assistant()
    {
        var client = await NewUserClientAsync();
        var (orgA, a) = await NewAssistantAsync(client, "A");
        var (orgB, _) = await NewAssistantAsync(client, "B");
        var docsOfA = new[]
        {
            await UploadOkAsync(client, orgA, "a1.txt", "banana laranja"),
            await UploadOkAsync(client, orgA, "a2.txt", "uva pera"),
        };
        var secretOfB = Marker();
        for (var i = 0; i < 6; i++)
            await UploadOkAsync(client, orgB, $"b{i}.txt", $"banana banana banana {secretOfB} {i}");
        var marker = Marker();

        var body = await JsonAsync(await AskAsync(client, a, $"banana {marker}"));

        Assert.All(body.GetProperty("sources").EnumerateArray(),
            s => Assert.Contains(s.GetProperty("documentId").GetGuid(), docsOfA));
        Assert.DoesNotContain(secretOfB, Factory.Chat.PromptContaining(marker));
    }

    // C24
    [Fact]
    public async Task Ask_prompt_contains_instructions_chunks_and_question()
    {
        var client = await NewUserClientAsync();
        var instructions = $"Responda sempre como um pirata {Marker()}";
        var (org, id) = await NewAssistantAsync(client, "Pirata", instructions);
        var texts = new[] { "o tesouro esta na ilha norte", "o mapa esta no navio" };
        foreach (var t in texts) await UploadOkAsync(client, org, $"{Guid.NewGuid():N}.txt", t);
        var question = $"onde esta o tesouro {Marker()}";

        var body = await JsonAsync(await AskAsync(client, id, question));

        Assert.Equal(2, body.GetProperty("sources").GetArrayLength());
        var prompt = Factory.Chat.PromptContaining(question);
        Assert.Contains(instructions, prompt);
        Assert.Contains(question, prompt);
        foreach (var t in texts) Assert.Contains(t, prompt);
    }

    // C25
    [Fact]
    public async Task Ask_without_documents_returns_empty_sources()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);

        var response = await AskAsync(client, id, "tem alguma coisa?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("sources").ValueKind);
        Assert.Equal(0, body.GetProperty("sources").GetArrayLength());
    }

    // C26
    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("   ", HttpStatusCode.BadRequest)]
    [InlineData("q2001", HttpStatusCode.BadRequest)]
    [InlineData("q2000", HttpStatusCode.OK)]
    public async Task Ask_question_bounds(string question, HttpStatusCode expected)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        if (question.StartsWith("q2")) question = new string('a', int.Parse(question[1..]));

        var response = await AskAsync(client, id, question);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
            Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("question", out _));
    }

    // C27
    [Theory]
    [InlineData(FakeAiTriggers.FailEmbedding)]
    [InlineData(FakeAiTriggers.FailChat)]
    public async Task Ask_provider_failure_returns_502(string trigger)
    {
        var client = await NewUserClientAsync();
        var (org, id) = await NewAssistantAsync(client);
        await UploadOkAsync(client, org, "a.txt", $"conteudo {Guid.NewGuid()}");

        var response = await AskAsync(client, id, $"pergunta {trigger}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
    }

    // C28
    [Fact]
    public async Task Ask_rate_limit_is_20_per_minute_per_user()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var other = await NewUserClientAsync();
        var otherId = await CreateAssistantAsync(other);

        for (var i = 1; i <= 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, id, $"pergunta {i}")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await AskAsync(client, id, "pergunta 21")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AskAsync(other, otherId, "pergunta do outro")).StatusCode);
    }

    // C10
    [Fact]
    public async Task Assistants_of_same_organization_share_documents()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "ACME");
        var direct = await CreateAssistantInAsync(client, organization, "Direto");
        var teacher = await CreateAssistantInAsync(client, organization, "Professor", "Explique como um professor");
        var document = await UploadOkAsync(client, organization, "manual.txt", $"politica de ferias {Marker()}");

        foreach (var assistant in new[] { direct, teacher })
        {
            var body = await JsonAsync(await AskAsync(client, assistant, "politica de ferias"));
            Assert.Contains(body.GetProperty("sources").EnumerateArray(), s => s.GetProperty("documentId").GetGuid() == document);
        }
    }

    // C11
    [Fact]
    public async Task Retrieves_only_from_own_organization()
    {
        var client = await NewUserClientAsync();
        var (acme, assistant) = await NewAssistantAsync(client, "ACME");
        var globex = await CreateOrganizationAsync(client, "Globex");
        var own = await UploadOkAsync(client, acme, "acme.txt", "reembolso de viagem aprovado pelo gestor");
        var secret = Marker();
        var foreign = new List<Guid>();
        for (var i = 0; i < 6; i++)
            foreign.Add(await UploadOkAsync(client, globex, $"globex{i}.txt", $"reembolso reembolso reembolso viagem {secret} {i}"));
        var marker = Marker();

        var body = await JsonAsync(await AskAsync(client, assistant, $"reembolso viagem {marker}"));

        var sources = body.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("documentId").GetGuid()).ToList();
        Assert.Equal([own], sources);
        Assert.DoesNotContain(secret, Factory.Chat.PromptContaining(marker));
    }
}
