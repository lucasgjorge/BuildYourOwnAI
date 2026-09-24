using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Documents;

public static class DeleteDocument
{
    public static void Map(RouteGroupBuilder group) => group.MapDelete("/{id:guid}/documents/{documentId:guid}", Handle);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id, Guid documentId, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Organizations.AnyAsync(o => o.Id == id, ct))
            return Problems.OrganizationNotFound();

        // Chunks go with the document through ON DELETE CASCADE (door 1).
        var deleted = await db.Documents
            .Where(d => d.Id == documentId && d.OrganizationId == id)
            .ExecuteDeleteAsync(ct);

        return deleted == 0 ? Problems.DocumentNotFound() : TypedResults.NoContent();
    }
}
