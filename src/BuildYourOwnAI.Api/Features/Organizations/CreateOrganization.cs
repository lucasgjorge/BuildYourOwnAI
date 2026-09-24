using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildYourOwnAI.Api.Features.Organizations;

public static class CreateOrganization
{
    public sealed record Request(string? Name);

    public sealed record Response(Guid Id, string Name, DateTimeOffset CreatedAt);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/", Handle);

    private static async Task<Results<Created<Response>, ValidationProblem>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > Organization.NameMaxLength)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = [$"O nome deve ter entre 1 e {Organization.NameMaxLength} caracteres."],
            });

        var organization = new Organization { OwnerId = currentUser.Id!, Name = name };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(
            $"/api/organizations/{organization.Id}",
            new Response(organization.Id, organization.Name, organization.CreatedAt));
    }
}
