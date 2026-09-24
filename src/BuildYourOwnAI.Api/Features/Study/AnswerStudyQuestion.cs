using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Study;

/// <summary>Grades one answer on the server and reveals the right option, the explanation and where it came from.</summary>
public static class AnswerStudyQuestion
{
    public sealed record Request(int? Option);

    public sealed record Source(Guid DocumentId, string FileName, int ChunkIndex);

    public sealed record Response(bool Correct, int ChosenOption, int CorrectOption, string Explanation, Source Source);

    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/study-sessions").RequireAuthorization().WithTags("Study");
        sessions.MapPost("/{sessionId:guid}/questions/{questionId:guid}/answer", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid sessionId, Guid questionId, Request request, AppDbContext db, CancellationToken ct)
    {
        if (request.Option is not { } option || option is < 0 or >= StudyQuestion.OptionCount)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["option"] = ["Escolha uma alternativa de 0 a 3."],
            });

        var question = await db.StudyQuestions
            .Where(q => q.Id == questionId && q.SessionId == sessionId)
            .Select(q => new { q.CorrectOption, q.ChosenOption, q.Explanation, q.DocumentId, q.Document.FileName, q.ChunkIndex })
            .FirstOrDefaultAsync(ct);
        if (question is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Pergunta não encontrada.");

        // Answered once: the update only lands while no answer is stored, so a second or concurrent answer loses.
        var saved = question.ChosenOption is null && await db.StudyQuestions
            .Where(q => q.Id == questionId && q.ChosenOption == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.ChosenOption, (short)option)
                .SetProperty(q => q.AnsweredAt, DateTimeOffset.UtcNow), ct) == 1;
        if (!saved)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Esta pergunta já foi respondida.");

        return TypedResults.Ok(new Response(
            option == question.CorrectOption, option, question.CorrectOption, question.Explanation,
            new Source(question.DocumentId, question.FileName, question.ChunkIndex)));
    }
}
