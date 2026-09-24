using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace BuildYourOwnAI.Api.Tests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected const string Password = "Passw0rd!";
    protected ApiFactory Factory { get; } = factory;

    protected static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    protected async Task<HttpClient> NewUserClientAsync()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();
        return client;
    }

    protected static async Task<Guid> CreateOrganizationAsync(HttpClient client, string name = "Minha organização")
    {
        var response = await client.PostAsJsonAsync("/api/organizations", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected static async Task<Guid> CreateAssistantInAsync(
        HttpClient client, Guid organizationId, string name = "Minha IA", string? instructions = null, string? routingDescription = null)
    {
        var response = await client.PostAsJsonAsync("/api/assistants", new { organizationId, name, instructions, routingDescription });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>An assistant alone in a new organization of its own - the rag-mvp shape.</summary>
    protected static async Task<(Guid Organization, Guid Assistant)> NewAssistantAsync(
        HttpClient client, string name = "Minha IA", string? instructions = null, string? routingDescription = null)
    {
        var organization = await CreateOrganizationAsync(client, name);
        return (organization, await CreateAssistantInAsync(client, organization, name, instructions, routingDescription));
    }

    protected static async Task<Guid> CreateAssistantAsync(HttpClient client, string name = "Minha IA", string? instructions = null) =>
        (await NewAssistantAsync(client, name, instructions)).Assistant;

    protected static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid organizationId, string fileName, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return client.PostAsync($"/api/organizations/{organizationId}/documents", form);
    }

    protected static Task<HttpResponseMessage> UploadTextAsync(HttpClient client, Guid organizationId, string fileName, string text) =>
        UploadAsync(client, organizationId, fileName, Encoding.UTF8.GetBytes(text));

    protected static async Task<Guid> UploadOkAsync(HttpClient client, Guid organizationId, string fileName, string text)
    {
        var response = await UploadTextAsync(client, organizationId, fileName, text);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected static Task<HttpResponseMessage> AskAsync(HttpClient client, Guid assistantId, string question) =>
        client.PostAsJsonAsync($"/api/assistants/{assistantId}/ask", new { question });

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    protected static byte[] PdfWithText(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    protected static byte[] PdfWithoutText()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    protected async Task<long> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    protected static Task<HttpResponseMessage> RouteAsync(HttpClient client, string question) =>
        client.PostAsJsonAsync("/api/route/ask", new { question });

    protected Task<long> DocumentCountAsync(Guid organizationId) =>
        ScalarAsync("select count(*) from documents where organization_id = @id", ("id", organizationId));

    protected Task<long> ChunkCountForOrganizationAsync(Guid organizationId) =>
        ScalarAsync("select count(*) from chunks c join documents d on d.id = c.document_id where d.organization_id = @id", ("id", organizationId));
}
