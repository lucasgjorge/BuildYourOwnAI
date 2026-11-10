using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class ConversationUsageTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Usage_is_aggregated_for_the_conversation_and_current_user_only()
    {
        var (client, _, _) = await NewUserAsync();
        var (_, assistant) = await NewAssistantAsync(client, routingDescription: "respostas curtas");
        var conversationId = Guid.CreateVersion7();

        var ask = await client.PostAsJsonAsync($"/api/assistants/{assistant}/ask", new
        {
            question = "pergunta direta",
            conversationId,
        });
        Assert.Equal(HttpStatusCode.OK, ask.StatusCode);

        var route = await client.PostAsJsonAsync("/api/route/ask", new
        {
            question = $"pergunta roteada {FakeAiTriggers.Route(1)}",
            conversationId,
        });
        Assert.Equal(HttpStatusCode.OK, route.StatusCode);

        var (otherClient, _, _) = await NewUserAsync();
        var otherAssistant = await CreateAssistantAsync(otherClient);
        var otherAsk = await otherClient.PostAsJsonAsync($"/api/assistants/{otherAssistant}/ask", new
        {
            question = "pergunta de outra conta",
            conversationId,
        });
        Assert.Equal(HttpStatusCode.OK, otherAsk.StatusCode);

        var usage = await client.GetAsync($"/api/usage/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.OK, usage.StatusCode);
        using var result = JsonDocument.Parse(await usage.Content.ReadAsStringAsync());
        Assert.Equal(304, result.RootElement.GetProperty("inputTokens").GetInt64());
        Assert.Equal(65, result.RootElement.GetProperty("outputTokens").GetInt64());
        Assert.Equal(369, result.RootElement.GetProperty("totalTokens").GetInt64());

        var emptyUsage = await client.GetAsync($"/api/usage/conversations/{Guid.CreateVersion7()}");
        using var emptyResult = JsonDocument.Parse(await emptyUsage.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyResult.RootElement.GetProperty("totalTokens").GetInt64());
    }

    [Fact]
    public async Task Usage_requires_authentication()
    {
        using var client = Factory.CreateHttpsClient();

        var response = await client.GetAsync($"/api/usage/conversations/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
