using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

/// <summary>Helpers shared by the study tests: documents with a known number of chunks, and the session routes.</summary>
public abstract class StudyTestBase(ApiFactory factory) : ApiTestBase(factory)
{
    protected static string Marker() => "m" + Guid.NewGuid().ToString("N")[..12];

    // 10-character words; the 1000/200 chunker cuts 100 words into 1 chunk, 300 into 4, 1700 into 21.
    protected static string Words(string seed, int count) => string.Join(' ', Enumerable.Range(0, count).Select(i => $"{seed}{i:D5}"));

    protected static string TextWithMarker(string marker, int words) => $"{marker} {Words(marker[..4], words)}";

    protected static Task<HttpResponseMessage> CreateSessionAsync(HttpClient client, Guid organization, int? count, IEnumerable<Guid>? documentIds = null) =>
        client.PostAsJsonAsync($"/api/organizations/{organization}/study-sessions", new { questionCount = count, documentIds = documentIds?.ToArray() });

    protected static Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid session, Guid question, int? option) =>
        client.PostAsJsonAsync($"/api/study-sessions/{session}/questions/{question}/answer", new { option });

    protected async Task<JsonElement> CreatedSessionAsync(HttpClient client, Guid organization, int count, IEnumerable<Guid>? documentIds = null)
    {
        var response = await CreateSessionAsync(client, organization, count, documentIds);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    protected async Task<List<object?[]>> RowsAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    protected Task<long> StudyCallsAsync() => Task.FromResult((long)Factory.Chat.Calls.Count(c => c.Any(m => m.Text.Contains(FakeChatClient.StudyPromptMarker))));

    protected async Task<T> WithStudyReplyAsync<T>(Func<string, string> reply, Func<Task<T>> call)
    {
        Factory.Chat.StudyReply = reply;
        try { return await call(); }
        finally { Factory.Chat.StudyReply = null; }
    }
}

public sealed class StudySessionsTests(ApiFactory factory) : StudyTestBase(factory)
{
    private static object Item(int chunk, string prompt = "Pergunta?", string[]? options = null, int correct = 0, string explanation = "porque sim") =>
        new { chunk, prompt, options = options ?? ["certa", "errada a", "errada b", "errada c"], correct, explanation };

    // C1
    [Fact]
    public async Task Create_returns_questions_with_four_options()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client, "Estudos");
        var a = await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 300));
        var b = await UploadOkAsync(client, organization, "b.txt", TextWithMarker(Marker(), 300));

        var response = await CreateSessionAsync(client, organization, 5, [a, b]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/study-sessions/{id}", response.Headers.Location?.OriginalString);
        Assert.True(body.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        var questions = body.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(5, questions.Count);
        Assert.Equal([1, 2, 3, 4, 5], questions.Select(q => q.GetProperty("position").GetInt32()));
        foreach (var q in questions)
        {
            Assert.NotEqual(Guid.Empty, q.GetProperty("id").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(q.GetProperty("prompt").GetString()));
            Assert.Equal(4, q.GetProperty("options").GetArrayLength());
        }
    }

    // C2
    [Fact]
    public async Task Samples_only_chosen_documents_without_repeating()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var (markerA, markerB) = (Marker(), Marker());
        var a = await UploadOkAsync(client, organization, "a.txt", TextWithMarker(markerA, 300));
        await UploadOkAsync(client, organization, "b.txt", TextWithMarker(markerB, 300));

        await CreatedSessionAsync(client, organization, 5, [a]);

        var prompt = Factory.Chat.PromptContaining(markerA[..4]);
        Assert.DoesNotContain(markerB[..4], prompt);
        var chunks = Regex.Matches(prompt, @"^\[\d+\] (.*)$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(4, chunks.Count);
        Assert.Equal(chunks.Count, chunks.Distinct().Count());

        var other = await NewUserClientAsync();
        var both = await CreateOrganizationAsync(other);
        var (markerC, markerD) = (Marker(), Marker());
        await UploadOkAsync(other, both, "c.txt", TextWithMarker(markerC, 50));
        await UploadOkAsync(other, both, "d.txt", TextWithMarker(markerD, 50));
        await CreatedSessionAsync(other, both, 5);
        var all = Factory.Chat.PromptContaining(markerC[..4]);
        Assert.Contains(markerD[..4], all);
    }

    // C3
    [Fact]
    public async Task Fewer_chunks_than_requested_caps_questions()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        for (var i = 0; i < 3; i++)
            await UploadOkAsync(client, organization, $"d{i}.txt", TextWithMarker(Marker(), 50));

        var body = await CreatedSessionAsync(client, organization, 10);

        Assert.Equal(3, body.GetProperty("questions").GetArrayLength());
    }

    // C4
    [Fact]
    public async Task Creation_never_reveals_the_answer()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 300));

        var body = await CreatedSessionAsync(client, organization, 5);

        foreach (var q in body.GetProperty("questions").EnumerateArray())
            foreach (var hidden in new[] { "correctOption", "correct", "explanation", "source", "documentId", "chunkIndex" })
                Assert.False(q.TryGetProperty(hidden, out _), hidden);
    }

    // C5
    [Theory]
    [InlineData(0, false, "questionCount")]
    [InlineData(3, false, "questionCount")]
    [InlineData(7, false, "questionCount")]
    [InlineData(21, false, "questionCount")]
    [InlineData(null, false, "questionCount")]
    [InlineData(5, true, "documentIds")]
    [InlineData(5, false, null)]
    [InlineData(10, false, null)]
    [InlineData(20, false, null)]
    public async Task Invalid_input_returns_400(int? count, bool emptyDocuments, string? field)
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 50));

        var response = await CreateSessionAsync(client, organization, count, emptyDocuments ? [] : null);

        if (field is null)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return;
        }
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    // C6
    [Theory]
    [InlineData("foreign-user")]
    [InlineData("missing-organization")]
    [InlineData("document-of-other-organization")]
    public async Task Foreign_or_missing_returns_404(string scenario)
    {
        var owner = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(owner);
        var marker = Marker();
        var document = await UploadOkAsync(owner, organization, "a.txt", TextWithMarker(marker, 50));
        var otherOrganization = await CreateOrganizationAsync(owner, "Outra");
        await UploadOkAsync(owner, otherOrganization, "b.txt", TextWithMarker(Marker(), 50));

        var response = scenario switch
        {
            "foreign-user" => await CreateSessionAsync(await NewUserClientAsync(), organization, 5),
            "missing-organization" => await CreateSessionAsync(owner, Guid.NewGuid(), 5),
            _ => await CreateSessionAsync(owner, otherOrganization, 5, [document]),
        };

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(Factory.Chat.ReceivedCallContaining(marker[..4]));
    }

    // C7
    [Fact]
    public async Task No_chunks_returns_422()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var before = await StudyCallsAsync();

        var response = await CreateSessionAsync(client, organization, 5);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await StudyCallsAsync());
    }

    public static TheoryData<string> InvalidKinds() =>
        ["missing-chunk", "repeated-chunk", "three-options", "five-options", "empty-option", "repeated-options", "correct-4", "correct-minus-1", "empty-prompt"];

    // C8
    [Theory]
    [MemberData(nameof(InvalidKinds))]
    public async Task Invalid_model_items_are_discarded(string kind)
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 50));
        await UploadOkAsync(client, organization, "b.txt", TextWithMarker(Marker(), 50));
        var invalid = kind switch
        {
            "missing-chunk" => Item(99),
            "repeated-chunk" => Item(1, prompt: "De novo o trecho 1?"),
            "three-options" => Item(2, options: ["a", "b", "c"]),
            "five-options" => Item(2, options: ["a", "b", "c", "d", "e"]),
            "empty-option" => Item(2, options: ["a", " ", "c", "d"]),
            "repeated-options" => Item(2, options: ["a", "b", "A", "d"]),
            "correct-4" => Item(2, correct: 4),
            "correct-minus-1" => Item(2, correct: -1),
            _ => Item(2, prompt: " "),
        };

        var body = await WithStudyReplyAsync(
            _ => JsonSerializer.Serialize(new { questions = new[] { Item(1, prompt: "Válida?"), invalid } }),
            () => CreatedSessionAsync(client, organization, 5));

        var question = Assert.Single(body.GetProperty("questions").EnumerateArray());
        Assert.Equal("Válida?", question.GetProperty("prompt").GetString());
    }

    // C9
    [Theory]
    [InlineData("throws")]
    [InlineData("not-json")]
    [InlineData("no-valid-item")]
    public async Task Model_failure_returns_502_without_saving(string failure)
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var text = TextWithMarker(Marker(), 50) + (failure == "throws" ? $" {FakeAiTriggers.FailChat}" : "");
        await UploadOkAsync(client, organization, "a.txt", text);

        var response = await WithStudyReplyAsync(
            prompt => failure switch
            {
                "not-json" => "aqui estão suas perguntas",
                "no-valid-item" => JsonSerializer.Serialize(new { questions = new[] { Item(1, correct: 9) } }),
                _ => FakeChatClient.DefaultStudyReply(prompt),
            },
            () => CreateSessionAsync(client, organization, 5));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await ScalarAsync("select count(*) from study_sessions where organization_id = @o", ("o", organization)));
    }

    // C10
    [Fact]
    public async Task Options_are_shuffled_keeping_the_answer()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "longo.txt", TextWithMarker(Marker(), 1700));

        var body = await CreatedSessionAsync(client, organization, 20);

        Assert.Equal(20, body.GetProperty("questions").GetArrayLength());
        var rows = await RowsAsync(
            "select options, correct_option from study_questions where session_id = @s",
            ("s", body.GetProperty("id").GetGuid()));
        Assert.Equal(20, rows.Count);
        foreach (var row in rows)
            Assert.StartsWith("certa ", ((string[])row[0]!)[(short)row[1]!]);
        Assert.True(rows.Select(r => (short)r[1]!).Distinct().Count() > 1);
    }

    // C11
    [Fact]
    public async Task Shares_rate_limit_with_ask()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client);
        await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 50));

        for (var i = 1; i <= 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, assistant, $"pergunta {i}")).StatusCode);
        for (var i = 11; i <= 20; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateSessionAsync(client, organization, 5)).StatusCode);

        var limited = await CreateSessionAsync(client, organization, 5);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    // C18
    [Fact]
    public async Task Schema_has_study_tables()
    {
        async Task<long> Count(string sql) => await ScalarAsync(sql);

        foreach (var (table, column) in new[] { ("study_sessions", "organization_id"), ("study_questions", "session_id"), ("study_questions", "document_id") })
            Assert.Equal(1, await Count($"""
                select count(*) from information_schema.referential_constraints rc
                join information_schema.key_column_usage k on k.constraint_name = rc.constraint_name
                where k.table_name = '{table}' and k.column_name = '{column}' and rc.delete_rule = 'CASCADE'
                """));
        foreach (var check in new[] { "ck_study_questions_options", "ck_study_questions_correct_option", "ck_study_questions_chosen_option" })
            Assert.Equal(1, await Count($"select count(*) from pg_constraint where conname = '{check}' and contype = 'c'"));
        Assert.Equal(1, await Count("""
            select count(*) from pg_indexes
            where tablename = 'study_questions' and indexdef like 'CREATE UNIQUE INDEX%(session_id, "position")%'
            """));
        Assert.Equal(1, await Count("select count(*) from information_schema.columns where table_name = 'study_questions' and column_name = 'options' and data_type = 'ARRAY'"));
    }

    // C19
    [Fact]
    public async Task Deletes_cascade_to_study_data()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var a = await UploadOkAsync(client, organization, "a.txt", TextWithMarker(Marker(), 50));
        var b = await UploadOkAsync(client, organization, "b.txt", TextWithMarker(Marker(), 50));
        var session = (await CreatedSessionAsync(client, organization, 5)).GetProperty("id").GetGuid();
        Assert.Equal(2, await ScalarAsync("select count(*) from study_questions where session_id = @s", ("s", session)));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/organizations/{organization}/documents/{a}")).StatusCode);

        Assert.Equal(0, await ScalarAsync("select count(*) from study_questions where document_id = @d", ("d", a)));
        Assert.Equal(1, await ScalarAsync("select count(*) from study_questions where document_id = @d", ("d", b)));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/organizations/{organization}")).StatusCode);
        Assert.Equal(0, await ScalarAsync("select count(*) from study_sessions where id = @s", ("s", session)));
    }

    // C21
    [Fact]
    public async Task Study_never_logs_content()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var chunkSecret = Marker();
        await UploadOkAsync(client, organization, "a.txt", TextWithMarker(chunkSecret, 50));
        var (promptSecret, optionSecret, explanationSecret) = (Marker(), Marker(), Marker());
        var before = Factory.Logs.Entries.Count;

        var body = await WithStudyReplyAsync(
            _ => JsonSerializer.Serialize(new { questions = new[] { Item(1, $"pergunta {promptSecret}", [$"certa {optionSecret}", "b", "c", "d"], 0, $"porque {explanationSecret}") } }),
            () => CreatedSessionAsync(client, organization, 5));
        var session = body.GetProperty("id").GetGuid();
        var question = body.GetProperty("questions")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await AnswerAsync(client, session, question, 1)).StatusCode);

        var logged = Factory.Logs.Entries.Skip(before)
            .SelectMany(e => e.Properties.Values.Select(v => v?.ToString() ?? "").Append(e.Message)).ToList();
        Assert.Contains(Factory.Logs.Entries.Skip(before), e => e.Properties.TryGetValue("SessionId", out var v) && v?.ToString() == session.ToString());
        foreach (var secret in new[] { chunkSecret, promptSecret, optionSecret, explanationSecret })
            Assert.DoesNotContain(logged, text => text.Contains(secret));
    }
}
