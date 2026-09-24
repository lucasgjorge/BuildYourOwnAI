using BuildYourOwnAI.Api.Features.Ask;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class AssistantsEndpoints
{
    public static IEndpointRouteBuilder MapAssistantsEndpoints(this IEndpointRouteBuilder app)
    {
        var assistants = app.MapGroup("/api/assistants").RequireAuthorization().WithTags("Assistants");

        CreateAssistant.Map(assistants);
        GetAssistant.Map(assistants);
        DeleteAssistant.Map(assistants);

        AskAssistant.Map(assistants);

        return app;
    }
}
