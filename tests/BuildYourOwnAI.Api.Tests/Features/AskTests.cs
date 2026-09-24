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
        var id = await CreateAssistantAsync(client);
        var docs = new Dictionary<Guid, int>();
        for (var k = 1; k <= 7; k++)
            docs[await UploadOkAsync(client, id, $"doc{k}.txt", GradedDoc(k))] = k;

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

        var small = await CreateAssistantAsync(client, "Pequena");
        await UploadOkAsync(client, small, "um.txt", GradedDoc(2));
        await UploadOkAsync(client, small, "dois.txt", GradedDoc(5));
        var smallSources = (await JsonAsync(await AskAsync(client, small, "alpha"))).GetProperty("sources");
        Assert.Equal(2, smallSources.GetArrayLength());
    }

    // C23
    [Fact]
    public async Task Ask_never_retrieves_from_another_assistant()
    {
        var client = await NewUserClientAsync();
        var a = await CreateAssistantAsync(client, "A");
        var b = await CreateAssistantAsync(client, "B");
        var docsOfA = new[]
        {
            await UploadOkAsync(client, a, "a1.txt", "banana laranja"),
            await UploadOkAsync(client, a, "a2.txt", "uva pera"),
        };
        var secretOfB = Marker();
        for (var i = 0; i < 6; i++)
            await UploadOkAsync(client, b, $"b{i}.txt", $"banana banana banana {secretOfB} {i}");
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
        var id = await CreateAssistantAsync(client, "Pirata", instructions);
        var texts = new[] { "o tesouro esta na ilha norte", "o mapa esta no navio" };
        foreach (var t in texts) await UploadOkAsync(client, id, $"{Guid.NewGuid():N}.txt", t);
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
        var id = await CreateAssistantAsync(client);
        await UploadOkAsync(client, id, "a.txt", $"conteudo {Guid.NewGuid()}");

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
}
