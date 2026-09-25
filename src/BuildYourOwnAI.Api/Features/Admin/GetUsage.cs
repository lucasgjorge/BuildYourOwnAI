using System.Globalization;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Features.Admin;

/// <summary>AI consumption and estimated cost of every user in a period, by user and by mode (admin-usage).</summary>
public static class GetUsage
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    public sealed record Totals(int Calls, int Actions, long InputTokens, long OutputTokens, decimal CostUsd, int UnpricedCalls);

    public sealed record UserUsage(string UserId, string? Email, int Calls, int Actions, long InputTokens, long OutputTokens, decimal CostUsd);

    public sealed record ModeUsage(string Mode, int Calls, int Actions, long InputTokens, long OutputTokens, decimal CostUsd);

    public sealed record Response(
        DateOnly From, DateOnly To, DateTimeOffset? Since, Totals Totals, IReadOnlyList<UserUsage> ByUser, IReadOnlyList<ModeUsage> ByMode);

    private sealed record Group<TKey>(TKey Key, int Calls, int Actions, long InputTokens, long OutputTokens, decimal CostUsd);

    public static void Map(RouteGroupBuilder admin) => admin.MapGet("/usage", Handle);

    private static async Task<IResult> Handle(string? from, string? to, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to is null ? today : ParseDate(to);
        if (end is null)
            errors["to"] = ["Use uma data válida no formato AAAA-MM-DD."];
        var start = from is null ? end?.AddDays(-(DefaultDays - 1)) : ParseDate(from);
        if (from is not null && start is null)
            errors["from"] = ["Use uma data válida no formato AAAA-MM-DD."];
        if (start is { } s && end is { } e)
        {
            if (s > e)
                errors["from"] = ["O início do período deve ser antes do fim."];
            else if (e.DayNumber - s.DayNumber + 1 > MaxDays)
                errors["to"] = [$"O período pode ter no máximo {MaxDays} dias."];
        }
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        // Whole UTC days: from 00:00 of the first to the end of the last.
        var startAt = new DateTimeOffset(start!.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endAt = new DateTimeOffset(end!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rows = db.AiUsages.Where(u => u.OccurredAt >= startAt && u.OccurredAt < endAt);

        var totals = await rows
            .GroupBy(_ => 1)
            .Select(g => new Totals(
                g.Count(),
                g.Select(u => u.CorrelationId).Distinct().Count(),
                g.Sum(u => (long)u.InputTokens),
                g.Sum(u => (long)u.OutputTokens),
                g.Sum(u => u.CostUsd) ?? 0,
                g.Count(u => u.CostUsd == null)))
            .FirstOrDefaultAsync(ct) ?? new Totals(0, 0, 0, 0, 0, 0);

        var byUserGroups = await rows
            .GroupBy(u => u.UserId)
            .Select(g => new Group<string>(
                g.Key, g.Count(), g.Select(u => u.CorrelationId).Distinct().Count(),
                g.Sum(u => (long)u.InputTokens), g.Sum(u => (long)u.OutputTokens), g.Sum(u => u.CostUsd) ?? 0))
            .ToListAsync(ct);
        var userIds = byUserGroups.Select(g => g.Key).ToList();
        var emails = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        var byUser = byUserGroups
            .Select(g => new UserUsage(g.Key, emails.GetValueOrDefault(g.Key), g.Calls, g.Actions, g.InputTokens, g.OutputTokens, g.CostUsd))
            .OrderByDescending(u => u.CostUsd)
            .ThenByDescending(u => u.InputTokens + u.OutputTokens)
            .ThenBy(u => u.Email, StringComparer.Ordinal)
            .ToList();

        var byModeGroups = await rows
            .GroupBy(u => u.Mode)
            .Select(g => new Group<UsageMode>(
                g.Key, g.Count(), g.Select(u => u.CorrelationId).Distinct().Count(),
                g.Sum(u => (long)u.InputTokens), g.Sum(u => (long)u.OutputTokens), g.Sum(u => u.CostUsd) ?? 0))
            .ToDictionaryAsync(g => g.Key, ct);
        // Every mode, even without use, so the screen can compare all of them.
        var byMode = Enum.GetValues<UsageMode>()
            .Select(m => byModeGroups.TryGetValue(m, out var g)
                ? new ModeUsage(ModeName(m), g.Calls, g.Actions, g.InputTokens, g.OutputTokens, g.CostUsd)
                : new ModeUsage(ModeName(m), 0, 0, 0, 0, 0))
            .OrderByDescending(m => m.Actions)
            .ThenByDescending(m => m.Calls)
            .ToList();

        var since = await db.AiUsages.MinAsync(u => (DateTimeOffset?)u.OccurredAt, ct);

        return TypedResults.Ok(new Response(start.Value, end.Value, since, totals, byUser, byMode));
    }

    private static string ModeName(UsageMode mode) => mode.ToString().ToLowerInvariant();

    private static DateOnly? ParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
