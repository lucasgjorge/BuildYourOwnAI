using System.Net;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class DocumentChunksTests(ApiFactory factory) : ApiTestBase(factory)
{
    // 300 ten-character words: 3000 chars, which the 1000/200 chunker cuts into 4 chunks.
    private static string FourChunkText(string seed) => string.Join(' ', Enumerable.Range(0, 300).Select(i => $"{seed}{i:D4}"[..9]));

    private async Task<(HttpClient Client, Guid Organization, Guid Document)> DocumentAsync()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var document = await UploadOkAsync(client, organization, "rh.txt", FourChunkText("w" + Guid.NewGuid().ToString("N")[..4]));
        Assert.Equal(4, await ScalarAsync("select chunk_count from documents where id = @d", ("d", document)));
        return (client, organization, document);
    }

    private async Task<Dictionary<int, string>> StoredChunksAsync(Guid document)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select index, content from chunks where document_id = @d", connection);
        command.Parameters.AddWithValue("d", document);
        var chunks = new Dictionary<int, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) chunks[reader.GetInt32(0)] = reader.GetString(1);
        return chunks;
    }

    private static List<int> Indexes(JsonElement body) => body.GetProperty("chunks").EnumerateArray().Select(c => c.GetProperty("index").GetInt32()).ToList();

    // C1
    [Fact]
    public async Task Returns_chunk_with_neighbors()
    {
        var (client, organization, document) = await DocumentAsync();
        var stored = await StoredChunksAsync(document);

        foreach (var query in new[] { "?around=1", "" })
        {
            var response = await client.GetAsync($"/api/organizations/{organization}/documents/{document}/chunks/1{query}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await JsonAsync(response);
            Assert.Equal(document, body.GetProperty("documentId").GetGuid());
            Assert.Equal("rh.txt", body.GetProperty("fileName").GetString());
            Assert.Equal(4, body.GetProperty("chunkCount").GetInt32());
            Assert.Equal([0, 1, 2], Indexes(body));
            foreach (var chunk in body.GetProperty("chunks").EnumerateArray())
                Assert.Equal(stored[chunk.GetProperty("index").GetInt32()], chunk.GetProperty("content").GetString());
        }
    }

    // C2
    [Theory]
    [InlineData(0, "", new[] { 0, 1 })]
    [InlineData(3, "", new[] { 2, 3 })]
    [InlineData(2, "?around=0", new[] { 2 })]
    [InlineData(1, "?around=2", new[] { 0, 1, 2, 3 })]
    public async Task Edges_return_only_existing_neighbors(int index, string query, int[] expected)
    {
        var (client, organization, document) = await DocumentAsync();

        var body = await JsonAsync(await client.GetAsync($"/api/organizations/{organization}/documents/{document}/chunks/{index}{query}"));

        Assert.Equal(expected, Indexes(body));
    }

    // C3
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task Around_out_of_range_returns_400(int around)
    {
        var (client, organization, document) = await DocumentAsync();

        var response = await client.GetAsync($"/api/organizations/{organization}/documents/{document}/chunks/1?around={around}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("around", out _));
    }

    // C4
    [Theory]
    [InlineData("foreign-user")]
    [InlineData("missing-organization")]
    [InlineData("other-organization")]
    [InlineData("index-too-high")]
    [InlineData("index-negative")]
    public async Task Foreign_or_missing_returns_404(string scenario)
    {
        var (client, organization, document) = await DocumentAsync();
        var otherOrganization = await CreateOrganizationAsync(client, "Outra");
        var requester = scenario == "foreign-user" ? await NewUserClientAsync() : client;
        var path = scenario switch
        {
            "missing-organization" => $"/api/organizations/{Guid.NewGuid()}/documents/{document}/chunks/1",
            "other-organization" => $"/api/organizations/{otherOrganization}/documents/{document}/chunks/1",
            "index-too-high" => $"/api/organizations/{organization}/documents/{document}/chunks/4",
            "index-negative" => $"/api/organizations/{organization}/documents/{document}/chunks/-1",
            _ => $"/api/organizations/{organization}/documents/{document}/chunks/1",
        };

        var response = await requester.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
