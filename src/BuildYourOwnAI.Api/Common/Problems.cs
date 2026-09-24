using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildYourOwnAI.Api.Common;

public static class Problems
{
    // Door 5: another user's assistant and a missing one are indistinguishable.
    public static ProblemHttpResult AssistantNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "IA não encontrada.");

    public static ProblemHttpResult DocumentNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Documento não encontrado.");
}
