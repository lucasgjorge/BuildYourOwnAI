using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class AssistantsTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C7
    [Theory]
    [InlineData(1, null)]
    [InlineData(100, null)]
    [InlineData(10, 4000)]
    public async Task Create_valid_returns_201_with_location(int nameLength, int? instructionsLength)
    {
        var client = await NewUserClientAsync();
        var name = new string('n', nameLength);
        var instructions = instructionsLength is null ? null : new string('i', instructionsLength.Value);

        var response = await client.PostAsJsonAsync("/api/assistants", new { name = "  " + name + "  ", instructions });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/assistants/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(instructions, body.GetProperty("instructions").GetString());
        Assert.True(body.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // C8
    [Theory]
    [InlineData("", 0, "name")]
    [InlineData("   ", 0, "name")]
    [InlineData(null, 0, "name")]
    [InlineData("n101", 0, "name")]
    [InlineData("ok", 4001, "instructions")]
    public async Task Create_invalid_returns_400_keyed_by_field(string? name, int instructionsLength, string field)
    {
        var client = await NewUserClientAsync();
        if (name == "n101") name = new string('n', 101);
        var instructions = instructionsLength == 0 ? null : new string('i', instructionsLength);

        var response = await client.PostAsJsonAsync("/api/assistants", new { name, instructions });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    // C9
    [Fact]
    public async Task List_returns_only_own_newest_first()
    {
        var client = await NewUserClientAsync();
        var other = await NewUserClientAsync();
        await CreateAssistantAsync(other, "de outro usuario");

        var empty = await client.GetAsync("/api/assistants");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(0, (await JsonAsync(empty)).GetArrayLength());

        var createdA = await JsonAsync(await client.PostAsJsonAsync("/api/assistants", new { name = "A", instructions = "instrucoes de A" }));
        var createdB = await JsonAsync(await client.PostAsJsonAsync("/api/assistants", new { name = "B" }));
        var a = createdA.GetProperty("id").GetGuid();
        var b = createdB.GetProperty("id").GetGuid();
        await UploadOkAsync(client, a, "a.txt", "conteudo do documento A");

        var list = await JsonAsync(await client.GetAsync("/api/assistants"));

        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(b, list[0].GetProperty("id").GetGuid());
        Assert.Equal(a, list[1].GetProperty("id").GetGuid());
        Assert.Equal(0, list[0].GetProperty("documentCount").GetInt32());
        Assert.Equal(1, list[1].GetProperty("documentCount").GetInt32());
        Assert.Equal("B", list[0].GetProperty("name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, list[0].GetProperty("instructions").ValueKind);
        Assert.Equal("A", list[1].GetProperty("name").GetString());
        Assert.Equal("instrucoes de A", list[1].GetProperty("instructions").GetString());
        // Postgres keeps microseconds, .NET ticks are 100 ns: the listed value must match creation within 1 ms.
        AssertSameInstant(createdB.GetProperty("createdAt").GetDateTimeOffset(), list[0].GetProperty("createdAt").GetDateTimeOffset());
        AssertSameInstant(createdA.GetProperty("createdAt").GetDateTimeOffset(), list[1].GetProperty("createdAt").GetDateTimeOffset());
    }

    public static TheoryData<string, string, bool> IdRoutes() => new()
    {
        { "GET", "/api/assistants/{id}", false },
        { "DELETE", "/api/assistants/{id}", false },
        { "POST", "/api/assistants/{id}/documents", false },
        { "GET", "/api/assistants/{id}/documents", false },
        { "DELETE", "/api/assistants/{id}/documents/{doc}", false },
        { "POST", "/api/assistants/{id}/ask", false },
        { "GET", "/api/assistants/{id}", true },
        { "DELETE", "/api/assistants/{id}", true },
        { "POST", "/api/assistants/{id}/documents", true },
        { "GET", "/api/assistants/{id}/documents", true },
        { "DELETE", "/api/assistants/{id}/documents/{doc}", true },
        { "POST", "/api/assistants/{id}/ask", true },
    };

    // C10
    [Theory]
    [MemberData(nameof(IdRoutes))]
    public async Task Foreign_or_missing_assistant_returns_404(string method, string template, bool missing)
    {
        var owner = await NewUserClientAsync();
        var foreignId = await CreateAssistantAsync(owner, "do dono");
        var foreignDoc = await UploadOkAsync(owner, foreignId, "dono.txt", $"documento do dono {Guid.NewGuid()}");

        var intruder = await NewUserClientAsync();
        var targetId = missing ? Guid.NewGuid() : foreignId;
        var path = template.Replace("{id}", targetId.ToString()).Replace("{doc}", foreignDoc.ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (path.EndsWith("/documents") && method == "POST")
        {
            var form = new MultipartFormDataContent { { new ByteArrayContent("intruso"u8.ToArray()), "file", "x.txt" } };
            request.Content = form;
        }
        else if (path.EndsWith("/ask"))
        {
            request.Content = JsonContent.Create(new { question = "o que tem ai?" });
        }

        var response = await intruder.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, await DocumentCountAsync(foreignId));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/assistants/{foreignId}")).StatusCode);
    }

    // C11
    [Fact]
    public async Task Delete_returns_204_and_cascades()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var docA = await UploadOkAsync(client, id, "a.txt", $"primeiro documento {Guid.NewGuid()}");
        var docB = await UploadOkAsync(client, id, "b.txt", $"segundo documento {Guid.NewGuid()}");
        const string chunksOfDocs = "select count(*) from chunks where document_id in (@a, @b)";
        Assert.True(await ScalarAsync(chunksOfDocs, ("a", docA), ("b", docB)) > 0);

        var response = await client.DeleteAsync($"/api/assistants/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await ScalarAsync("select count(*) from assistants where id = @id", ("id", id)));
        Assert.Equal(0, await DocumentCountAsync(id));
        Assert.Equal(0, await ScalarAsync(chunksOfDocs, ("a", docA), ("b", docB)));
    }

    // C44
    [Fact]
    public async Task Get_returns_200_with_document_count()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client, "Suporte", "Responda em portugues");
        await UploadOkAsync(client, id, "a.txt", $"um {Guid.NewGuid()}");
        await UploadOkAsync(client, id, "b.txt", $"dois {Guid.NewGuid()}");

        var response = await client.GetAsync($"/api/assistants/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Suporte", body.GetProperty("name").GetString());
        Assert.Equal("Responda em portugues", body.GetProperty("instructions").GetString());
        var listed = (await JsonAsync(await client.GetAsync("/api/assistants")))[0];
        AssertSameInstant(listed.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(2, body.GetProperty("documentCount").GetInt32());
    }

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange((actual - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
}
