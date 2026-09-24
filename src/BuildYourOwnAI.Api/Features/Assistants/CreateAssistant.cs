using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class CreateAssistant
{
    public sealed record Request(string? Name, string? Instructions);

    public sealed record Response(Guid Id, string Name, string? Instructions, DateTimeOffset CreatedAt);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/", Handle);

    private static async Task<Results<Created<Response>, ValidationProblem>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions;

        var errors = new Dictionary<string, string[]>();
        if (name.Length is 0 or > Assistant.NameMaxLength)
            errors["name"] = [$"O nome deve ter entre 1 e {Assistant.NameMaxLength} caracteres."];
        if (instructions?.Length > Assistant.InstructionsMaxLength)
            errors["instructions"] = [$"As instruções devem ter no máximo {Assistant.InstructionsMaxLength} caracteres."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var assistant = new Assistant { OwnerId = currentUser.Id!, Name = name, Instructions = instructions };
        db.Assistants.Add(assistant);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(
            $"/api/assistants/{assistant.Id}",
            new Response(assistant.Id, assistant.Name, assistant.Instructions, assistant.CreatedAt));
    }
}
