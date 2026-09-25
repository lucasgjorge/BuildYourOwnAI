using System.Text;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BuildYourOwnAI.Api.Features.Gaps;

public static class AnswerGap
{
    public const int AnswerMaxLength = 4000;
    private const int FileNameQuestionLength = 60;

    public sealed record Request(string? Answer, Guid? OrganizationId);

    public sealed record Response(Guid Id, string Status, Guid DocumentId);

    public static void Map(RouteGroupBuilder group) => group.MapPost("/{id:guid}/answer", Handle);

    private static async Task<IResult> Handle(
        Guid id,
        Request request,
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IUsageRecorder usage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(AnswerGap));
        var answer = request.Answer?.Trim() ?? "";
        if (answer.Length is 0 or > AnswerMaxLength)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answer"] = [$"A resposta deve ter entre 1 e {AnswerMaxLength} caracteres."],
            });

        var gap = await db.Gaps.Where(g => g.Id == id).Select(g => new { g.Status, g.OrganizationId, g.Question }).FirstOrDefaultAsync(ct);
        if (gap is null)
            return Problems.GapNotFound();
        if (gap.Status != GapStatus.Open)
            return GapsEndpoints.AlreadyClosed();

        // A gap the all-assistants chat could not route has no organization; the owner picks where the answer goes.
        var organizationId = gap.OrganizationId ?? request.OrganizationId;
        if (organizationId is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["organizationId"] = ["Escolha a organização que vai guardar esta resposta."],
            });
        if (gap.OrganizationId is null && !await db.Organizations.AnyAsync(o => o.Id == organizationId, ct))
            return Problems.OrganizationNotFound();

        // Door 7: the answer becomes an ordinary document of the organization, retrieved and cited like any other.
        var question = gap.Question;
        var fileName = $"Lacuna - {(question.Length <= FileNameQuestionLength ? question : question[..FileNameQuestionLength])}.md";
        var content = Encoding.UTF8.GetBytes($"# Pergunta\n{question}\n\n# Resposta\n{answer}\n");
        var (document, failure) = await DocumentIngestion.PrepareAsync(db, embeddings, usage, UsageMode.Gap, organizationId.Value, fileName, content, logger, ct);
        if (failure is not null)
            return failure;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Documents.Add(document!);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DocumentIngestion.IsDuplicate(ex))
        {
            return DocumentIngestion.AlreadyAttached();
        }

        // Only an open gap closes; a concurrent answer or dismiss that got there first wins.
        var closed = await db.Gaps
            .Where(g => g.Id == id && g.Status == GapStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GapStatus.Answered).SetProperty(g => g.DocumentId, document!.Id), ct);
        if (closed == 0)
            return GapsEndpoints.AlreadyClosed();

        await transaction.CommitAsync(ct);
        logger.LogInformation("Gap {GapId} answered with document {DocumentId}", id, document!.Id);

        return TypedResults.Ok(new Response(id, "answered", document.Id));
    }
}
