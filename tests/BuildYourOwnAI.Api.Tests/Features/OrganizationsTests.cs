using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class OrganizationsTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C1
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Create_valid_returns_201(int nameLength)
    {
        var client = await NewUserClientAsync();
        var name = new string('o', nameLength);

        var response = await client.PostAsJsonAsync("/api/organizations", new { name = "  " + name + "  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/organizations/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // C2
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("o101")]
    public async Task Create_invalid_returns_400(string name)
    {
        var client = await NewUserClientAsync();
        if (name == "o101") name = new string('o', 101);

        var response = await client.PostAsJsonAsync("/api/organizations", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("name", out _));
    }

    // C3
    [Fact]
    public async Task List_returns_only_own_newest_first_with_counts()
    {
        var client = await NewUserClientAsync();
        var other = await NewUserClientAsync();
        await CreateOrganizationAsync(other, "de outro usuario");

        var empty = await client.GetAsync("/api/organizations");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(0, (await JsonAsync(empty)).GetArrayLength());

        var createdA = await JsonAsync(await client.PostAsJsonAsync("/api/organizations", new { name = "A" }));
        var createdB = await JsonAsync(await client.PostAsJsonAsync("/api/organizations", new { name = "B" }));
        var a = createdA.GetProperty("id").GetGuid();
        var b = createdB.GetProperty("id").GetGuid();
        await CreateAssistantInAsync(client, a, "Direto");
        await CreateAssistantInAsync(client, a, "Professor");
        await UploadOkAsync(client, a, "a.txt", $"conteudo de A {Guid.NewGuid()}");

        var list = await JsonAsync(await client.GetAsync("/api/organizations"));

        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(b, list[0].GetProperty("id").GetGuid());
        Assert.Equal(a, list[1].GetProperty("id").GetGuid());
        Assert.Equal("B", list[0].GetProperty("name").GetString());
        Assert.Equal("A", list[1].GetProperty("name").GetString());
        Assert.Equal(0, list[0].GetProperty("assistantCount").GetInt32());
        Assert.Equal(2, list[1].GetProperty("assistantCount").GetInt32());
        Assert.Equal(0, list[0].GetProperty("documentCount").GetInt32());
        Assert.Equal(1, list[1].GetProperty("documentCount").GetInt32());
        AssertSameInstant(createdB.GetProperty("createdAt").GetDateTimeOffset(), list[0].GetProperty("createdAt").GetDateTimeOffset());
        AssertSameInstant(createdA.GetProperty("createdAt").GetDateTimeOffset(), list[1].GetProperty("createdAt").GetDateTimeOffset());
    }

    // C4
    [Fact]
    public async Task Get_returns_assistants_and_document_count()
    {
        var client = await NewUserClientAsync();
        var created = await JsonAsync(await client.PostAsJsonAsync("/api/organizations", new { name = "ACME" }));
        var id = created.GetProperty("id").GetGuid();
        var direct = await CreateAssistantInAsync(client, id, "Direto", routingDescription: "respostas curtas");
        var teacher = await CreateAssistantInAsync(client, id, "Professor");
        await UploadOkAsync(client, id, "a.txt", $"um {Guid.NewGuid()}");
        await UploadOkAsync(client, id, "b.txt", $"dois {Guid.NewGuid()}");

        var response = await client.GetAsync($"/api/organizations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("ACME", body.GetProperty("name").GetString());
        AssertSameInstant(created.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(2, body.GetProperty("documentCount").GetInt32());
        var assistants = body.GetProperty("assistants");
        Assert.Equal(2, assistants.GetArrayLength());
        Assert.Equal(direct, assistants[0].GetProperty("id").GetGuid());
        Assert.Equal("Direto", assistants[0].GetProperty("name").GetString());
        Assert.Equal("respostas curtas", assistants[0].GetProperty("routingDescription").GetString());
        Assert.Equal(teacher, assistants[1].GetProperty("id").GetGuid());
        Assert.Equal("Professor", assistants[1].GetProperty("name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, assistants[1].GetProperty("routingDescription").ValueKind);
    }

    public static TheoryData<string, string, bool> IdRoutes()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var missing in new[] { false, true })
        {
            data.Add("GET", "/api/organizations/{id}", missing);
            data.Add("DELETE", "/api/organizations/{id}", missing);
            data.Add("POST", "/api/organizations/{id}/documents", missing);
            data.Add("GET", "/api/organizations/{id}/documents", missing);
            data.Add("DELETE", "/api/organizations/{id}/documents/{doc}", missing);
        }
        return data;
    }

    // C5
    [Theory]
    [MemberData(nameof(IdRoutes))]
    public async Task Foreign_or_missing_organization_returns_404(string method, string template, bool missing)
    {
        var owner = await NewUserClientAsync();
        var foreignId = await CreateOrganizationAsync(owner, "do dono");
        var foreignDoc = await UploadOkAsync(owner, foreignId, "dono.txt", $"documento do dono {Guid.NewGuid()}");

        var intruder = await NewUserClientAsync();
        var path = template.Replace("{id}", (missing ? Guid.NewGuid() : foreignId).ToString()).Replace("{doc}", foreignDoc.ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = new MultipartFormDataContent { { new ByteArrayContent("intruso"u8.ToArray()), "file", "x.txt" } };

        var response = await intruder.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, await DocumentCountAsync(foreignId));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/organizations/{foreignId}")).StatusCode);
    }

    // C12
    [Fact]
    public async Task Delete_returns_204_and_cascades()
    {
        var client = await NewUserClientAsync();
        var (organization, assistant) = await NewAssistantAsync(client, "ACME");
        var (keptOrganization, keptAssistant) = await NewAssistantAsync(client, "Globex");
        var document = await UploadOkAsync(client, organization, "a.txt", $"documento {Guid.NewGuid()}");
        await UploadOkAsync(client, keptOrganization, "b.txt", $"documento mantido {Guid.NewGuid()}");
        await AskAsync(client, assistant, $"pergunta sem resposta {FakeAiTriggers.NotFound}");
        await AskAsync(client, keptAssistant, $"outra sem resposta {FakeAiTriggers.NotFound}");
        Assert.True(await ScalarAsync("select count(*) from chunks where document_id = @d", ("d", document)) > 0);

        var response = await client.DeleteAsync($"/api/organizations/{organization}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await ScalarAsync("select count(*) from organizations where id = @id", ("id", organization)));
        Assert.Equal(0, await ScalarAsync("select count(*) from assistants where organization_id = @id", ("id", organization)));
        Assert.Equal(0, await DocumentCountAsync(organization));
        Assert.Equal(0, await ScalarAsync("select count(*) from chunks where document_id = @d", ("d", document)));
        Assert.Equal(0, await ScalarAsync("select count(*) from gaps where organization_id = @id", ("id", organization)));
        Assert.Equal(1, await ScalarAsync("select count(*) from assistants where organization_id = @id", ("id", keptOrganization)));
        Assert.Equal(1, await DocumentCountAsync(keptOrganization));
        Assert.Equal(1, await ScalarAsync("select count(*) from gaps where organization_id = @id", ("id", keptOrganization)));
    }

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange((actual - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
}
