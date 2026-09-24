using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class AssistantsTests(ApiFactory factory) : ApiTestBase(factory)
{
    // C6 (rag-mvp C7 bounds kept)
    [Theory]
    [InlineData(1, null, null)]
    [InlineData(100, null, null)]
    [InlineData(10, 4000, 500)]
    public async Task Create_valid_returns_201_with_location(int nameLength, int? instructionsLength, int? routingLength)
    {
        var client = await NewUserClientAsync();
        var organizationId = await CreateOrganizationAsync(client);
        var name = new string('n', nameLength);
        var instructions = instructionsLength is null ? null : new string('i', instructionsLength.Value);
        var routingDescription = routingLength is null ? null : new string('r', routingLength.Value);

        var response = await client.PostAsJsonAsync("/api/assistants",
            new { organizationId, name = "  " + name + "  ", instructions, routingDescription });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/assistants/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(organizationId, body.GetProperty("organizationId").GetGuid());
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(instructions, body.GetProperty("instructions").GetString());
        Assert.Equal(routingDescription, body.GetProperty("routingDescription").GetString());
        if (routingDescription is null)
            Assert.Equal(JsonValueKind.Null, body.GetProperty("routingDescription").ValueKind);
        Assert.True(body.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // C7 (rag-mvp C8 kept)
    [Theory]
    [InlineData("", 0, 0, true, "name")]
    [InlineData("   ", 0, 0, true, "name")]
    [InlineData(null, 0, 0, true, "name")]
    [InlineData("n101", 0, 0, true, "name")]
    [InlineData("ok", 4001, 0, true, "instructions")]
    [InlineData("ok", 0, 501, true, "routingDescription")]
    [InlineData("ok", 0, 0, false, "organizationId")]
    public async Task Create_invalid_returns_400_keyed_by_field(
        string? name, int instructionsLength, int routingLength, bool withOrganization, string field)
    {
        var client = await NewUserClientAsync();
        Guid? organizationId = withOrganization ? await CreateOrganizationAsync(client) : null;
        if (name == "n101") name = new string('n', 101);
        var instructions = instructionsLength == 0 ? null : new string('i', instructionsLength);
        var routingDescription = routingLength == 0 ? null : new string('r', routingLength);

        var response = await client.PostAsJsonAsync("/api/assistants", new { organizationId, name, instructions, routingDescription });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    // C8
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_in_foreign_or_missing_organization_returns_404(bool missing)
    {
        var owner = await NewUserClientAsync();
        var foreign = await CreateOrganizationAsync(owner);
        var intruder = await NewUserClientAsync();
        var before = await ScalarAsync("select count(*) from assistants");

        var response = await intruder.PostAsJsonAsync("/api/assistants",
            new { organizationId = missing ? Guid.NewGuid() : foreign, name = "intrusa" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await ScalarAsync("select count(*) from assistants"));
    }

    // C9
    [Fact]
    public async Task Get_returns_organization_and_routing_description()
    {
        var client = await NewUserClientAsync();
        var organizationId = await CreateOrganizationAsync(client, "ACME");
        var created = await JsonAsync(await client.PostAsJsonAsync("/api/assistants", new
        {
            organizationId, name = "Professor", instructions = "Explique com calma", routingDescription = "quando a pessoa quer entender",
        }));
        var id = created.GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/assistants/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(organizationId, body.GetProperty("organizationId").GetGuid());
        Assert.Equal("ACME", body.GetProperty("organizationName").GetString());
        Assert.Equal("Professor", body.GetProperty("name").GetString());
        Assert.Equal("Explique com calma", body.GetProperty("instructions").GetString());
        Assert.Equal("quando a pessoa quer entender", body.GetProperty("routingDescription").GetString());
        AssertSameInstant(created.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("createdAt").GetDateTimeOffset());
    }

    public static TheoryData<string, string, bool> IdRoutes() => new()
    {
        { "GET", "/api/assistants/{id}", false },
        { "DELETE", "/api/assistants/{id}", false },
        { "POST", "/api/assistants/{id}/ask", false },
        { "GET", "/api/assistants/{id}", true },
        { "DELETE", "/api/assistants/{id}", true },
        { "POST", "/api/assistants/{id}/ask", true },
    };

    // C62 (rag-mvp C10 for the assistant routes that remain)
    [Theory]
    [MemberData(nameof(IdRoutes))]
    public async Task Foreign_or_missing_assistant_returns_404(string method, string template, bool missing)
    {
        var owner = await NewUserClientAsync();
        var (organization, foreignId) = await NewAssistantAsync(owner, "do dono");
        await UploadOkAsync(owner, organization, "dono.txt", $"documento do dono {Guid.NewGuid()}");

        var intruder = await NewUserClientAsync();
        var path = template.Replace("{id}", (missing ? Guid.NewGuid() : foreignId).ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (path.EndsWith("/ask"))
            request.Content = JsonContent.Create(new { question = "o que tem ai?" });

        var response = await intruder.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, await DocumentCountAsync(organization));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/assistants/{foreignId}")).StatusCode);
    }

    // Door 1: deleting an assistant leaves the organization's documents in place.
    [Fact]
    public async Task Delete_returns_204_and_keeps_organization_documents()
    {
        var client = await NewUserClientAsync();
        var (organization, id) = await NewAssistantAsync(client);
        await UploadOkAsync(client, organization, "a.txt", $"primeiro documento {Guid.NewGuid()}");

        var response = await client.DeleteAsync($"/api/assistants/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await ScalarAsync("select count(*) from assistants where id = @id", ("id", id)));
        Assert.Equal(1, await DocumentCountAsync(organization));
    }

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange((actual - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
}
