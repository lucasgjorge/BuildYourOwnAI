using System.Text.RegularExpressions;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Common;

/// <summary>Opens a gap for an unanswered question, or counts one more ask on the open gap it matches.</summary>
public static partial class GapRecorder
{
    public static string Normalize(string question) => Whitespace().Replace(question.Trim().ToLowerInvariant(), " ");

    // Door 6: the partial unique index decides, so two identical questions at once still end in one gap.
    internal static async Task RecordAsync(
        AppDbContext db, string ownerId, Guid? organizationId, Guid? assistantId, string question, ILogger logger, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ids = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO gaps (id, owner_id, organization_id, assistant_id, question, normalized_question, ask_count, first_asked_at, last_asked_at, status)
            VALUES ({Guid.CreateVersion7()}, {ownerId}, {organizationId}, {assistantId}, {question}, {Normalize(question)}, 1, {now}, {now}, 'open')
            ON CONFLICT (owner_id, organization_id, normalized_question) WHERE status = 'open'
            DO UPDATE SET ask_count = gaps.ask_count + 1, last_asked_at = EXCLUDED.last_asked_at
            RETURNING id AS "Value"
            """).ToListAsync(ct);

        logger.LogInformation("Gap {GapId} recorded for organization {OrganizationId}", ids.Single(), organizationId);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
