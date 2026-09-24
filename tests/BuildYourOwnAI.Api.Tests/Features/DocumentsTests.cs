using System.Net;
using System.Net.Http.Json;
using System.Text;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class DocumentsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int MaxBytes = 10_485_760;

    private static string LongText(string seed, int paragraphs = 6) =>
        string.Join("\n\n", Enumerable.Range(0, paragraphs).Select(i =>
            $"Paragrafo {i} de {seed}. " + string.Concat(Enumerable.Repeat($"Frase numero {i} sobre o assunto {seed}. ", 20))));

    // C12
    [Theory]
    [InlineData("manual.txt")]
    [InlineData("notas.md")]
    [InlineData("contrato.pdf")]
    public async Task Upload_supported_file_returns_201_and_persists_chunks(string fileName)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var seed = Guid.NewGuid().ToString("N");
        var bytes = fileName.EndsWith(".pdf")
            ? PdfWithText($"Clausula {seed} do contrato de teste")
            : Encoding.UTF8.GetBytes(LongText(seed));

        var response = await UploadAsync(client, id, fileName, bytes);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var documentId = body.GetProperty("id").GetGuid();
        var chunkCount = body.GetProperty("chunkCount").GetInt32();
        Assert.True(chunkCount > 0);
        Assert.Equal(fileName, body.GetProperty("fileName").GetString());
        Assert.Equal(bytes.Length, body.GetProperty("sizeBytes").GetInt64());
        Assert.True(body.TryGetProperty("uploadedAt", out _));
        Assert.Equal(chunkCount, await ScalarAsync("select count(*) from chunks where document_id = @d", ("d", documentId)));
        Assert.Equal(chunkCount, await ScalarAsync(
            "select count(*) from chunks where document_id = @d and vector_dims(embedding) = 1536", ("d", documentId)));
    }

    // C13
    [Theory]
    [InlineData("programa.exe", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("relatorio.docx", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("semextensao", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("NOTAS.TXT", HttpStatusCode.Created)]
    public async Task Upload_extension_is_checked_case_insensitively(string fileName, HttpStatusCode expected)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);

        var response = await UploadTextAsync(client, id, fileName, $"conteudo {Guid.NewGuid()}");

        Assert.Equal(expected, response.StatusCode);
    }

    // C14
    [Theory]
    [InlineData(MaxBytes + 1, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(MaxBytes, HttpStatusCode.Created)]
    public async Task Upload_size_limit_is_10485760_bytes(int size, HttpStatusCode expected)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var head = Encoding.UTF8.GetBytes($"conteudo real {Guid.NewGuid()} ");
        var bytes = new byte[size];
        Array.Fill(bytes, (byte)' ');
        head.CopyTo(bytes, 0);

        var response = await UploadAsync(client, id, "grande.txt", bytes);

        Assert.Equal(expected, response.StatusCode);
    }

    // C15
    [Theory]
    [InlineData("no-file-part")]
    [InlineData("empty-file")]
    [InlineData("not-multipart")]
    public async Task Upload_without_file_returns_400(string scenario)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var url = $"/api/assistants/{id}/documents";

        var response = scenario switch
        {
            "no-file-part" => await client.PostAsync(url, new MultipartFormDataContent { { new StringContent("x"), "other" } }),
            "empty-file" => await UploadAsync(client, id, "vazio.txt", []),
            _ => await client.PostAsJsonAsync(url, new { file = "x" }),
        };

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // C16
    [Theory]
    [InlineData("txt")]
    [InlineData("pdf")]
    public async Task Upload_without_text_returns_422(string kind)
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);

        var response = kind == "txt"
            ? await UploadTextAsync(client, id, "branco.txt", "   \n\n\t  \r\n ")
            : await UploadAsync(client, id, "escaneado.pdf", PdfWithoutText());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await DocumentCountAsync(id));
    }

    // C17
    [Fact]
    public async Task Upload_duplicate_content_returns_409()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var other = await CreateAssistantAsync(client, "Outra");
        var text = $"conteudo identico {Guid.NewGuid()}";

        Assert.Equal(HttpStatusCode.Created, (await UploadTextAsync(client, id, "a.txt", text)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadTextAsync(client, id, "outro-nome.txt", text)).StatusCode);
        Assert.Equal(1, await DocumentCountAsync(id));
        Assert.Equal(HttpStatusCode.Created, (await UploadTextAsync(client, other, "a.txt", text)).StatusCode);
    }

    // C18
    [Fact]
    public async Task Upload_embedding_failure_returns_502_and_persists_nothing()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var text = LongText(Guid.NewGuid().ToString("N")) + " " + FakeAiTriggers.FailEmbedding;

        var response = await UploadTextAsync(client, id, "falha.txt", text);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await DocumentCountAsync(id));
        Assert.Equal(0, await ChunkCountForAssistantAsync(id));
    }

    // C19
    [Fact]
    public async Task List_returns_newest_first()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var first = await UploadOkAsync(client, id, "primeiro.txt", $"um {Guid.NewGuid()}");
        var second = await UploadOkAsync(client, id, "segundo.txt", $"dois {Guid.NewGuid()}");

        var response = await client.GetAsync($"/api/assistants/{id}/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await JsonAsync(response);
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(second, list[0].GetProperty("id").GetGuid());
        Assert.Equal(first, list[1].GetProperty("id").GetGuid());
        foreach (var field in new[] { "fileName", "sizeBytes", "chunkCount", "uploadedAt" })
            Assert.True(list[0].TryGetProperty(field, out _), field);
        Assert.Equal("segundo.txt", list[0].GetProperty("fileName").GetString());
    }

    // C20
    [Fact]
    public async Task Delete_returns_204_and_removes_from_retrieval()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        var removed = await UploadOkAsync(client, id, "remover.txt", "girafa girafa girafa savana");
        await UploadOkAsync(client, id, "manter.txt", "elefante elefante savana");

        var response = await client.DeleteAsync($"/api/assistants/{id}/documents/{removed}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await ScalarAsync("select count(*) from chunks where document_id = @d", ("d", removed)));
        var ask = await JsonAsync(await AskAsync(client, id, "girafa"));
        Assert.DoesNotContain(ask.GetProperty("sources").EnumerateArray(), s => s.GetProperty("documentId").GetGuid() == removed);
    }

    // C21
    [Fact]
    public async Task Delete_document_of_other_assistant_returns_404()
    {
        var client = await NewUserClientAsync();
        var a = await CreateAssistantAsync(client, "A");
        var b = await CreateAssistantAsync(client, "B");
        var docOfB = await UploadOkAsync(client, b, "b.txt", $"do B {Guid.NewGuid()}");

        var response = await client.DeleteAsync($"/api/assistants/{a}/documents/{docOfB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, await DocumentCountAsync(b));
    }

    // C37
    [Fact]
    public async Task Concurrent_duplicate_uploads_yield_one_201_one_409()
    {
        var client = await NewUserClientAsync();
        var id = await CreateAssistantAsync(client);
        // The rendezvous marker holds both requests inside embedding generation, i.e. after the duplicate pre-check,
        // so only the unique index can decide which one wins.
        var text = $"{FakeAiTriggers.Rendezvous} {LongText(Guid.NewGuid().ToString("N"))}";

        var results = await Task.WhenAll(
            UploadTextAsync(client, id, "a.txt", text),
            UploadTextAsync(client, id, "b.txt", text));

        var statuses = results.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
        Assert.Equal(1, await DocumentCountAsync(id));
    }
}
