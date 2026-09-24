using System.Net;
using System.Net.Http.Json;
using System.Text;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class SchemaTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<string?> StringScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    // C36
    [Fact]
    public async Task Chunk_embedding_is_vector1536_with_hnsw_cosine_index()
    {
        Factory.CreateHttpsClient(); // boots the app, which applies migrations

        var type = await StringScalarAsync(
            "select format_type(atttypid, atttypmod) from pg_attribute where attrelid = 'chunks'::regclass and attname = 'embedding'");
        var indexes = await StringScalarAsync(
            "select string_agg(indexdef, ' | ') from pg_indexes where tablename = 'chunks'");

        Assert.Equal("vector(1536)", type);
        Assert.NotNull(indexes);
        Assert.Contains(indexes!.Split(" | "), def => def.Contains("USING hnsw") && def.Contains("(embedding vector_cosine_ops)"));
    }
}

public sealed class ProblemDetailsTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C39
    [Fact]
    public async Task Error_responses_are_problem_json()
    {
        var client = await NewUserClientAsync();
        var (org, id) = await NewAssistantAsync(client);
        var text = $"conteudo {Guid.NewGuid()}";
        await UploadOkAsync(client, org, "a.txt", text);
        var tooBig = new byte[10_485_761];
        Array.Fill(tooBig, (byte)'a');

        var responses = new Dictionary<int, HttpResponseMessage>
        {
            [400] = await client.PostAsJsonAsync("/api/assistants", new { name = "" }),
            [404] = await client.GetAsync($"/api/assistants/{Guid.NewGuid()}"),
            [409] = await UploadTextAsync(client, org, "b.txt", text),
            [413] = await UploadAsync(client, org, "grande.txt", tooBig),
            [415] = await UploadTextAsync(client, org, "x.exe", "abc"),
            [422] = await UploadTextAsync(client, org, "branco.txt", "   "),
            [502] = await AskAsync(client, id, $"pergunta {FakeAiTriggers.FailChat}"),
        };
        HttpResponseMessage? limited = null;
        for (var i = 0; i < 25 && limited is null; i++)
        {
            var r = await AskAsync(client, id, $"pergunta {i}");
            if (r.StatusCode == HttpStatusCode.TooManyRequests) limited = r;
        }
        responses[429] = limited ?? throw new Xunit.Sdk.XunitException("no 429 after 25 asks");

        foreach (var (status, response) in responses)
        {
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(status, (await JsonAsync(response)).GetProperty("status").GetInt32());
        }
    }
}

public sealed class ObservabilityTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C40
    [Fact]
    public async Task Ingestion_logs_ids_and_counts_never_content()
    {
        var client = await NewUserClientAsync();
        var (org, id) = await NewAssistantAsync(client);
        var documentSecret = "segredo" + Guid.NewGuid().ToString("N");
        var questionSecret = "pergunta" + Guid.NewGuid().ToString("N");

        var documentId = await UploadOkAsync(client, org, "a.txt", $"conteudo confidencial {documentSecret}");
        var ask = await JsonAsync(await AskAsync(client, id, $"o que e {questionSecret}?"));
        var answer = ask.GetProperty("answer").GetString()!;

        var ingestion = Factory.Logs.Entries
            .Where(e => e.Properties.TryGetValue("DocumentId", out var v) && v?.ToString() == documentId.ToString())
            .ToList();
        var entry = Assert.Single(ingestion);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.True(entry.Properties.ContainsKey("ChunkCount"));
        Assert.True(entry.Properties.ContainsKey("ElapsedMs"));

        // Both the rendered message and every structured property value are checked: a sink may store either.
        var logged = Factory.Logs.Entries
            .SelectMany(e => e.Properties.Values.Select(v => v?.ToString() ?? "").Append(e.Message))
            .ToList();
        Assert.DoesNotContain(logged, text => text.Contains(documentSecret));
        Assert.DoesNotContain(logged, text => text.Contains(questionSecret));
        Assert.DoesNotContain(logged, text => text.Contains(answer));
    }

    // C50
    [Fact]
    public async Task Jev_and_gaps_never_log_content()
    {
        var client = await NewUserClientAsync();
        var (_, assistant) = await NewAssistantAsync(client, "ACME", routingDescription: "respostas curtas");
        string Secret() => "segredo" + Guid.NewGuid().ToString("N");
        var askSecret = Secret();
        var routedSecret = Secret();
        var noMatchSecret = Secret();
        var gapAnswerSecret = Secret();
        var before = Factory.Logs.Entries.Count;

        var unanswered = $"pergunta {askSecret} {FakeAiTriggers.NotFound}";
        await AskAsync(client, assistant, unanswered);
        var afterAsk = Factory.Logs.Entries.Count;
        await JevAsync(client, $"pergunta {routedSecret} {FakeAiTriggers.Route(1)} {FakeAiTriggers.Found}");
        await JevAsync(client, $"pergunta {noMatchSecret} {FakeAiTriggers.Route("NONE")}");
        var gaps = await JsonAsync(await client.GetAsync("/api/gaps"));
        var gap = gaps.EnumerateArray().Single(g => g.GetProperty("question").GetString() == unanswered).GetProperty("id").GetGuid();
        var answered = await client.PostAsJsonAsync($"/api/gaps/{gap}/answer", new { answer = $"resposta {gapAnswerSecret}" });
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);

        var entries = Factory.Logs.Entries.Skip(before).ToList();
        // The gap-creation log itself: written by the unanswered ask, before the gap is answered.
        Assert.Contains(Factory.Logs.Entries.Skip(before).Take(afterAsk - before),
            e => e.Properties.TryGetValue("GapId", out var v) && v?.ToString() == gap.ToString());
        var logged = entries.SelectMany(e => e.Properties.Values.Select(v => v?.ToString() ?? "").Append(e.Message)).ToList();
        foreach (var secret in new[] { askSecret, routedSecret, noMatchSecret, gapAnswerSecret, FakeAiTriggers.FoundAnswer, FakeRouterClient.OutputMarker })
            Assert.DoesNotContain(logged, text => text.Contains(secret));
    }
}

public sealed class SpaHostingTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C41
    [Theory]
    [InlineData("/")]
    [InlineData("/assistants/x")]
    public async Task Spa_fallback_serves_index_but_not_for_api(string path)
    {
        var client = Factory.CreateHttpsClient();

        var page = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains(ApiFactory.SpaMarker, await page.Content.ReadAsStringAsync());

        var api = await client.GetAsync("/api/nao-existe");
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        Assert.Equal("application/problem+json", api.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(ApiFactory.SpaMarker, await api.Content.ReadAsStringAsync());
    }
}

public sealed class ArchitectureTests
{
    // C38
    [Fact]
    public void Features_do_not_reference_OpenAI()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BuildYourOwnAI.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var features = Path.Combine(dir!.FullName, "src", "BuildYourOwnAI.Api", "Features");
        var files = Directory.GetFiles(features, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var offenders = files.Where(f => File.ReadAllText(f, Encoding.UTF8).Contains("OpenAI")).ToList();

        Assert.Empty(offenders);
    }
}
