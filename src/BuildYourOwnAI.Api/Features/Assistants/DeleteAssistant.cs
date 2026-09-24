using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Assistants;

public static class DeleteAssistant
{
    public static void Map(RouteGroupBuilder group) => group.MapDelete("/{id:guid}", Handle);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        // Documents belong to the organization and stay; its gaps stay with assistant_id set to null (door 6).
        var deleted = await db.Assistants.Where(a => a.Id == id).ExecuteDeleteAsync(ct);

        return deleted == 0 ? Problems.AssistantNotFound() : TypedResults.NoContent();
    }
}
