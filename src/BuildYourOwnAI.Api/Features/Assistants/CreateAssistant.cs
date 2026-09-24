using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class CreateAssistant
{
    public sealed record Request(Guid? OrganizationId, string? Name, string? Instructions, string? RoutingDescription);

    public sealed record Response(
        Guid Id, Guid OrganizationId, string Name, string? Instructions, string? RoutingDescription, DateTimeOffset CreatedAt);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/", Handle);

    private static async Task<Results<Created<Response>, ValidationProblem, ProblemHttpResult>> Handle(
        Request request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions;
        var routingDescription = string.IsNullOrWhiteSpace(request.RoutingDescription) ? null : request.RoutingDescription.Trim();

        var errors = new Dictionary<string, string[]>();
        if (request.OrganizationId is null)
            errors["organizationId"] = ["Informe a organização da IA."];
        if (name.Length is 0 or > Assistant.NameMaxLength)
            errors["name"] = [$"O nome deve ter entre 1 e {Assistant.NameMaxLength} caracteres."];
        if (instructions?.Length > Assistant.InstructionsMaxLength)
            errors["instructions"] = [$"As instruções devem ter no máximo {Assistant.InstructionsMaxLength} caracteres."];
        if (routingDescription?.Length > Assistant.RoutingDescriptionMaxLength)
            errors["routingDescription"] = [$"'Quando usar esta IA' deve ter no máximo {Assistant.RoutingDescriptionMaxLength} caracteres."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var organizationId = request.OrganizationId!.Value;
        if (!await db.Organizations.AnyAsync(o => o.Id == organizationId, ct))
            return Problems.OrganizationNotFound();

        var assistant = new Assistant
        {
            OrganizationId = organizationId,
            Name = name,
            Instructions = instructions,
            RoutingDescription = routingDescription,
        };
        db.Assistants.Add(assistant);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(
            $"/api/assistants/{assistant.Id}",
            new Response(assistant.Id, assistant.OrganizationId, assistant.Name, assistant.Instructions, assistant.RoutingDescription, assistant.CreatedAt));
    }
}
