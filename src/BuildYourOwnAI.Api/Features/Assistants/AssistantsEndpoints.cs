using BuildYourOwnAI.Api.Features.Ask;
using BuildYourOwnAI.Api.Features.Documents;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class AssistantsEndpoints
{
    public static IEndpointRouteBuilder MapAssistantsEndpoints(this IEndpointRouteBuilder app)
    {
        var assistants = app.MapGroup("/api/assistants").RequireAuthorization().WithTags("Assistants");

        CreateAssistant.Map(assistants);
        ListAssistants.Map(assistants);
        GetAssistant.Map(assistants);
        DeleteAssistant.Map(assistants);

        UploadDocument.Map(assistants);
        ListDocuments.Map(assistants);
        DeleteDocument.Map(assistants);

        AskAssistant.Map(assistants);

        return app;
    }
}
