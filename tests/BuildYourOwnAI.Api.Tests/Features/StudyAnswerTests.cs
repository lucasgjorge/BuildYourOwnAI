using System.Net;
using System.Text.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class StudyAnswerTests(ApiFactory factory) : StudyTestBase(factory)
{
    private sealed record Stored(Guid Session, Guid Question, int Correct, Guid Document, int ChunkIndex);

    private async Task<(HttpClient Client, Stored Question)> QuestionAsync()
    {
        var client = await NewUserClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await UploadOkAsync(client, organization, "apostila.txt", TextWithMarker(Marker(), 50));
        var body = await CreatedSessionAsync(client, organization, 5);
        var session = body.GetProperty("id").GetGuid();
        var question = body.GetProperty("questions")[0].GetProperty("id").GetGuid();
        var row = (await RowsAsync("select correct_option, document_id, chunk_index from study_questions where id = @q", ("q", question))).Single();
        return (client, new Stored(session, question, (short)row[0]!, (Guid)row[1]!, (int)row[2]!));
    }

    private Task<object?> ChosenAsync(Guid question) =>
        RowsAsync("select chosen_option from study_questions where id = @q", ("q", question)).ContinueWith(t => t.Result.Single()[0]);

    private static void AssertSource(JsonElement body, Stored stored)
    {
        var source = body.GetProperty("source");
        Assert.Equal(stored.Document, source.GetProperty("documentId").GetGuid());
        Assert.Equal("apostila.txt", source.GetProperty("fileName").GetString());
        Assert.Equal(stored.ChunkIndex, source.GetProperty("chunkIndex").GetInt32());
    }

    // C12
    [Fact]
    public async Task Right_answer_is_graded_correct()
    {
        var (client, stored) = await QuestionAsync();

        var response = await AnswerAsync(client, stored.Session, stored.Question, stored.Correct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("correct").GetBoolean());
        Assert.Equal(stored.Correct, body.GetProperty("chosenOption").GetInt32());
        Assert.Equal(stored.Correct, body.GetProperty("correctOption").GetInt32());
        Assert.Equal("explicação 1", body.GetProperty("explanation").GetString());
        AssertSource(body, stored);
        Assert.Equal((short)stored.Correct, await ChosenAsync(stored.Question));
        Assert.Equal(1, await ScalarAsync("select count(*) from study_questions where id = @q and answered_at is not null", ("q", stored.Question)));
    }

    // C13
    [Fact]
    public async Task Wrong_answer_returns_the_right_one()
    {
        var (client, stored) = await QuestionAsync();
        var wrong = (stored.Correct + 1) % 4;

        var body = await JsonAsync(await AnswerAsync(client, stored.Session, stored.Question, wrong));

        Assert.False(body.GetProperty("correct").GetBoolean());
        Assert.Equal(wrong, body.GetProperty("chosenOption").GetInt32());
        Assert.Equal(stored.Correct, body.GetProperty("correctOption").GetInt32());
        Assert.Equal("explicação 1", body.GetProperty("explanation").GetString());
        AssertSource(body, stored);
    }

    // C14
    [Fact]
    public async Task Second_answer_returns_409()
    {
        var (client, stored) = await QuestionAsync();
        var first = (stored.Correct + 1) % 4;
        await AnswerAsync(client, stored.Session, stored.Question, first);

        var again = await AnswerAsync(client, stored.Session, stored.Question, stored.Correct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);
        Assert.Equal((short)first, await ChosenAsync(stored.Question));
    }

    // C15
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(null)]
    public async Task Option_out_of_range_returns_400(int? option)
    {
        var (client, stored) = await QuestionAsync();

        var response = await AnswerAsync(client, stored.Session, stored.Question, option);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("option", out _));
        Assert.Null(await ChosenAsync(stored.Question));
    }

    // C16
    [Theory]
    [InlineData("foreign-user")]
    [InlineData("missing-session")]
    [InlineData("question-of-other-session")]
    public async Task Foreign_or_missing_returns_404(string scenario)
    {
        var (client, stored) = await QuestionAsync();
        var (_, otherSession) = await QuestionAsync();

        var response = scenario switch
        {
            "foreign-user" => await AnswerAsync(await NewUserClientAsync(), stored.Session, stored.Question, 0),
            "missing-session" => await AnswerAsync(client, Guid.NewGuid(), stored.Question, 0),
            _ => await AnswerAsync(client, otherSession.Session, stored.Question, 0),
        };

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(await ChosenAsync(stored.Question));
    }
}
