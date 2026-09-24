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
        if (!await db.Assistants.AnyAsync(a => a.Id == id, ct))
            return Problems.AssistantNotFound();

        // Chunks go with the document through ON DELETE CASCADE (door 3).
        var deleted = await db.Documents
            .Where(d => d.Id == documentId && d.AssistantId == id)
            .ExecuteDeleteAsync(ct);

        return deleted == 0 ? Problems.DocumentNotFound() : TypedResults.NoContent();
    }
}
