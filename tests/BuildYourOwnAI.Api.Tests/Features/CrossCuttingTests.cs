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
        var id = await CreateAssistantAsync(client);
        var text = $"conteudo {Guid.NewGuid()}";
        await UploadOkAsync(client, id, "a.txt", text);
        var tooBig = new byte[10_485_761];
        Array.Fill(tooBig, (byte)'a');

        var responses = new Dictionary<int, HttpResponseMessage>
        {
            [400] = await client.PostAsJsonAsync("/api/assistants", new { name = "" }),
            [404] = await client.GetAsync($"/api/assistants/{Guid.NewGuid()}"),
            [409] = await UploadTextAsync(client, id, "b.txt", text),
            [413] = await UploadAsync(client, id, "grande.txt", tooBig),
            [415] = await UploadTextAsync(client, id, "x.exe", "abc"),
            [422] = await UploadTextAsync(client, id, "branco.txt", "   "),
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
        var id = await CreateAssistantAsync(client);
        var documentSecret = "segredo" + Guid.NewGuid().ToString("N");
        var questionSecret = "pergunta" + Guid.NewGuid().ToString("N");

        var documentId = await UploadOkAsync(client, id, "a.txt", $"conteudo confidencial {documentSecret}");
        var ask = await JsonAsync(await AskAsync(client, id, $"o que e {questionSecret}?"));
        var answer = ask.GetProperty("answer").GetString()!;

        var ingestion = Factory.Logs.Entries
            .Where(e => e.Properties.TryGetValue("DocumentId", out var v) && v?.ToString() == documentId.ToString())
            .ToList();
        var entry = Assert.Single(ingestion);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.True(entry.Properties.ContainsKey("ChunkCount"));
        Assert.True(entry.Properties.ContainsKey("ElapsedMs"));

        var all = Factory.Logs.Entries.ToList();
        Assert.DoesNotContain(all, e => e.Message.Contains(documentSecret));
        Assert.DoesNotContain(all, e => e.Message.Contains(questionSecret));
        Assert.DoesNotContain(all, e => e.Message.Contains(answer));
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
