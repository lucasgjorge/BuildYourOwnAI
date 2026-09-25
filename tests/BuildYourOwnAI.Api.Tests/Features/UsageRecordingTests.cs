using System.Net;
using System.Net.Http.Json;
using System.Text;
using BuildYourOwnAI.Api.Common;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class UsageRecordingTests(ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record UsageRow(string CorrelationId, string Mode, string Operation, string Model, int InputTokens, int OutputTokens, decimal? CostUsd);

    private async Task<List<UsageRow>> UsageAsync(string userId)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select correlation_id, mode, operation, model, input_tokens, output_tokens, cost_usd from ai_usage where user_id = @u order by occurred_at, id", connection);
        command.Parameters.AddWithValue("u", userId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<UsageRow>();
        while (await reader.ReadAsync())
            rows.Add(new UsageRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)));
        return rows;
    }

    private static Task<HttpResponseMessage> PostWithCorrelationAsync(HttpClient client, string path, object body, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Correlation-ID", correlationId);
        return client.SendAsync(request);
    }

    private static string Correlation() => "acao-teste-" + Guid.NewGuid().ToString("N");

    /// <summary>Text the chunker cuts into exactly <paramref name="chunks"/> chunks, ending with <paramref name="tail"/>.</summary>
    private static string TextWithChunks(int chunks, string tail = "")
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var text = new StringBuilder();
        var word = 0;
        while (TextChunker.Split(text + tail).Count < chunks)
            for (var i = 0; i < 40; i++)
                text.Append($"p{prefix}{word++:D6} ");
        var result = text + tail;
        Assert.Equal(chunks, TextChunker.Split(result).Count);
        return result;
    }

    // C1
    [Fact]
    public async Task Ask_records_embedding_and_chat()
    {
        var (client, _, userId) = await NewUserAsync();
        var assistant = await CreateAssistantAsync(client);

        var response = await AskAsync(client, assistant, "qual o prazo?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await UsageAsync(userId);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Select(r => r.CorrelationId).Distinct());
        Assert.All(rows, r => Assert.Equal("ask", r.Mode));
        var embedding = Assert.Single(rows, r => r.Operation == "embedding");
        Assert.Equal((FakeEmbeddingGenerator.Model, 7, 0), (embedding.Model, embedding.InputTokens, embedding.OutputTokens));
        var chat = Assert.Single(rows, r => r.Operation == "chat");
        Assert.Equal((FakeChatClient.Model, 120, 30), (chat.Model, chat.InputTokens, chat.OutputTokens));
    }

    // C2
    [Fact]
    public async Task Routing_records_choice_embedding_and_chat()
    {
        var (client, _, userId) = await NewUserAsync();
        var (organization, _) = await NewAssistantAsync(client, "ACME", routingDescription: "respostas curtas");

        foreach (var path in new[] { "/api/route/ask", $"/api/organizations/{organization}/route/ask" })
        {
            var correlation = Correlation();
            var response = await PostWithCorrelationAsync(client, path, new { question = $"pergunta {FakeAiTriggers.Route(1)}" }, correlation);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("answered", (await JsonAsync(response)).GetProperty("kind").GetString());
            var rows = (await UsageAsync(userId)).Where(r => r.CorrelationId == correlation).ToList();
            Assert.Equal(["choice", "embedding", "chat"], rows.Select(r => r.Operation));
            Assert.All(rows, r => Assert.Equal("routing", r.Mode));
            Assert.Equal((FakeRouterClient.Model, 50, 5), (rows[0].Model, rows[0].InputTokens, rows[0].OutputTokens));
        }
        Assert.Equal(6, (await UsageAsync(userId)).Count);
    }

    // C3
    [Fact]
    public async Task Upload_records_one_embedding_per_batch()
    {
        var (client, _, userId) = await NewUserAsync();
        var organization = await CreateOrganizationAsync(client);

        var response = await UploadTextAsync(client, organization, "grande.txt", TextWithChunks(150));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var rows = await UsageAsync(userId);
        Assert.Equal([700, 350], rows.Select(r => r.InputTokens));
        Assert.All(rows, r => Assert.Equal(("upload", "embedding", FakeEmbeddingGenerator.Model), (r.Mode, r.Operation, r.Model)));
    }

    // C4
    [Fact]
    public async Task Gap_answer_records_embedding()
    {
        var (client, _, userId) = await NewUserAsync();
        var assistant = await CreateAssistantAsync(client);
        var question = $"pergunta sem resposta {FakeAiTriggers.NotFound}";
        await AskAsync(client, assistant, question);
        var gap = (await JsonAsync(await client.GetAsync("/api/gaps"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        var correlation = Correlation();

        var response = await PostWithCorrelationAsync(client, $"/api/gaps/{gap}/answer", new { answer = "a resposta" }, correlation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await UsageAsync(userId), r => r.CorrelationId == correlation);
        Assert.Equal(("gap", "embedding"), (row.Mode, row.Operation));
    }

    // C5
    [Fact]
    public async Task Study_records_chat()
    {
        var (client, _, userId) = await NewUserAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "a.txt", $"conteudo de estudo {Guid.NewGuid()}");
        var correlation = Correlation();

        var response = await PostWithCorrelationAsync(client, $"/api/organizations/{organization}/study-sessions", new { questionCount = 5 }, correlation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = Assert.Single(await UsageAsync(userId), r => r.CorrelationId == correlation);
        Assert.Equal(("study", "chat", FakeChatClient.Model), (row.Mode, row.Operation, row.Model));
    }

    // C6
    [Fact]
    public async Task Cost_uses_pricing_or_is_null()
    {
        var (client, _, userId) = await NewUserAsync();
        var (_, assistant) = await NewAssistantAsync(client, "ACME", routingDescription: "respostas curtas");

        await AskAsync(client, assistant, "qual o prazo?");
        await RouteAsync(client, $"pergunta {FakeAiTriggers.Route(1)}");

        var rows = await UsageAsync(userId);
        Assert.All(rows.Where(r => r.Operation == "chat"), r => Assert.Equal(0.000096m, r.CostUsd));
        Assert.All(rows.Where(r => r.Operation == "embedding"), r => Assert.Equal(0.000070m, r.CostUsd));
        Assert.Null(Assert.Single(rows, r => r.Operation == "choice").CostUsd);
        Assert.Equal(2, rows.Count(r => r.Operation == "chat"));
    }

    // C7
    [Fact]
    public async Task Failed_call_records_nothing()
    {
        var (client, _, userId) = await NewUserAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME", routingDescription: "respostas curtas");

        var ask = Correlation();
        Assert.Equal(HttpStatusCode.BadGateway,
            (await PostWithCorrelationAsync(client, $"/api/assistants/{assistant}/ask", new { question = $"x {FakeAiTriggers.FailChat}" }, ask)).StatusCode);
        var upload = await UploadTextAsync(client, organization, "falha.txt", $"conteudo {FakeAiTriggers.FailEmbedding}");
        Assert.Equal(HttpStatusCode.BadGateway, upload.StatusCode);
        var route = Correlation();
        var routed = await PostWithCorrelationAsync(client, "/api/route/ask", new { question = $"x {FakeAiTriggers.Route("THROW")}" }, route);
        Assert.Equal("clarify", (await JsonAsync(routed)).GetProperty("kind").GetString());

        var rows = await UsageAsync(userId);
        Assert.Equal(["embedding"], rows.Where(r => r.CorrelationId == ask).Select(r => r.Operation));
        Assert.DoesNotContain(rows, r => r.Mode == "upload");
        Assert.DoesNotContain(rows, r => r.CorrelationId == route);
        Assert.Single(rows);
    }

    // C37
    [Fact]
    public async Task Partial_upload_records_only_successful_batches()
    {
        var (client, _, userId) = await NewUserAsync();
        var organization = await CreateOrganizationAsync(client);

        var response = await UploadTextAsync(client, organization, "parcial.txt", TextWithChunks(150, FakeAiTriggers.FailEmbedding));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var row = Assert.Single(await UsageAsync(userId));
        Assert.Equal(("upload", 700), (row.Mode, row.InputTokens));
    }

    // C8
    [Fact]
    public async Task Recording_failure_does_not_fail_request()
    {
        var (client, _, userId) = await NewUserAsync();
        var assistant = await CreateAssistantAsync(client);
        var constraint = "test_reject_" + Guid.NewGuid().ToString("N");
        await ExecAsync($"alter table ai_usage add constraint {constraint} check (user_id <> '{userId}') not valid");
        try
        {
            var before = Factory.Logs.Entries.Count;

            var response = await AskAsync(client, assistant, $"pergunta {FakeAiTriggers.Found}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(FakeAiTriggers.FoundAnswer, (await JsonAsync(response)).GetProperty("answer").GetString());
            Assert.Empty(await UsageAsync(userId));
            Assert.Contains(Factory.Logs.Entries.Skip(before),
                e => e.Category == typeof(UsageRecorder).FullName && e.Level == LogLevel.Warning);
        }
        finally
        {
            await ExecAsync($"alter table ai_usage drop constraint {constraint}");
        }
    }

    // C9
    [Fact]
    public async Task Usage_never_stores_or_logs_content()
    {
        var (client, _, userId) = await NewUserAsync();
        string Secret() => "segredo" + Guid.NewGuid().ToString("N");
        var secrets = Enumerable.Range(0, 6).Select(_ => Secret()).ToArray();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME", routingDescription: $"respostas {secrets[0]}");
        var before = Factory.Logs.Entries.Count;

        await UploadOkAsync(client, organization, "a.txt", $"documento {secrets[1]}");
        await AskAsync(client, assistant, $"pergunta {secrets[2]} {FakeAiTriggers.NotFound}");
        await RouteAsync(client, $"pergunta {secrets[3]} {FakeAiTriggers.Route(1)} {FakeAiTriggers.Found}");
        var gap = (await JsonAsync(await client.GetAsync("/api/gaps"))).EnumerateArray().First().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/gaps/{gap}/answer", new { answer = $"resposta {secrets[4]}" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync($"/api/organizations/{organization}/study-sessions", new { questionCount = 5 })).StatusCode);

        var rows = await UsageAsync(userId);
        Assert.Equal(["ask", "gap", "routing", "study", "upload"], rows.Select(r => r.Mode).Distinct().Order());
        var stored = rows.SelectMany(r => new[] { r.CorrelationId, r.Mode, r.Operation, r.Model }).ToList();
        var logged = Factory.Logs.Entries.Skip(before).SelectMany(e => e.Properties.Values.Select(v => v?.ToString() ?? "").Append(e.Message)).ToList();
        foreach (var marker in secrets.Take(5).Append(FakeAiTriggers.FoundAnswer).Append("certa 1").Append("explicação 1"))
        {
            Assert.DoesNotContain(stored, text => text.Contains(marker));
            Assert.DoesNotContain(logged, text => text.Contains(marker));
        }
    }

    // C12
    [Fact]
    public async Task Correlation_id_from_header_or_generated()
    {
        var (client, _, userId) = await NewUserAsync();
        var assistant = await CreateAssistantAsync(client);
        var path = $"/api/assistants/{assistant}/ask";

        var generated = await client.PostAsJsonAsync(path, new { question = "um" });
        var generatedId = Assert.Single(generated.Headers.GetValues("X-Correlation-ID"));
        Assert.True(Guid.TryParse(generatedId, out _));

        var sent = Correlation();
        var echoed = await PostWithCorrelationAsync(client, path, new { question = "dois" }, sent);
        Assert.Equal(sent, Assert.Single(echoed.Headers.GetValues("X-Correlation-ID")));

        var tooLong = new string('x', 65);
        var replaced = await PostWithCorrelationAsync(client, path, new { question = "tres" }, tooLong);
        var replacedId = Assert.Single(replaced.Headers.GetValues("X-Correlation-ID"));
        Assert.True(Guid.TryParse(replacedId, out _));

        var rows = await UsageAsync(userId);
        Assert.Equal(2, rows.Count(r => r.CorrelationId == generatedId));
        Assert.Equal(2, rows.Count(r => r.CorrelationId == sent));
        Assert.Equal(2, rows.Count(r => r.CorrelationId == replacedId));
        Assert.Equal(6, rows.Count);
    }

    private async Task ExecAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
