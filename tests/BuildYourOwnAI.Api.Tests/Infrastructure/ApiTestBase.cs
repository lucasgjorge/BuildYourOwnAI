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

    protected static async Task<Guid> CreateAssistantAsync(HttpClient client, string name = "Minha IA", string? instructions = null)
    {
        var response = await client.PostAsJsonAsync("/api/assistants", new { name, instructions });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid assistantId, string fileName, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return client.PostAsync($"/api/assistants/{assistantId}/documents", form);
    }

    protected static Task<HttpResponseMessage> UploadTextAsync(HttpClient client, Guid assistantId, string fileName, string text) =>
        UploadAsync(client, assistantId, fileName, Encoding.UTF8.GetBytes(text));

    protected static async Task<Guid> UploadOkAsync(HttpClient client, Guid assistantId, string fileName, string text)
    {
        var response = await UploadTextAsync(client, assistantId, fileName, text);
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

    protected Task<long> DocumentCountAsync(Guid assistantId) =>
        ScalarAsync("select count(*) from documents where assistant_id = @id", ("id", assistantId));

    protected Task<long> ChunkCountForAssistantAsync(Guid assistantId) =>
        ScalarAsync("select count(*) from chunks c join documents d on d.id = c.document_id where d.assistant_id = @id", ("id", assistantId));
}
