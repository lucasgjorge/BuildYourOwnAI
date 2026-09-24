using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildYourOwnAI.Api.Common;

public static class Problems
{
    // Another user's resource and a missing one are indistinguishable (AD-010).
    public static ProblemHttpResult OrganizationNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Organização não encontrada.");

    public static ProblemHttpResult AssistantNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "IA não encontrada.");

    public static ProblemHttpResult DocumentNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Documento não encontrado.");

    public static ProblemHttpResult GapNotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Lacuna não encontrada.");
}
