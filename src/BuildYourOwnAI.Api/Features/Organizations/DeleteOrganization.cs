using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Organizations;

public static class DeleteOrganization
{
    public static void Map(RouteGroupBuilder group) => group.MapDelete("/{id:guid}", Handle);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        // Assistants, documents, chunks and gaps go with it through ON DELETE CASCADE (door 1).
        var deleted = await db.Organizations.Where(o => o.Id == id).ExecuteDeleteAsync(ct);

        return deleted == 0 ? Problems.OrganizationNotFound() : TypedResults.NoContent();
    }
}
