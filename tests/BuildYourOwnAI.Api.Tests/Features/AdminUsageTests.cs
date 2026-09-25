using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

/// <summary>Report rows are inserted in 2001, a period no other test writes to; each test uses its own month.</summary>
public sealed class AdminUsageTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task InsertAsync(string userId, string occurredAt, string correlation, string mode, int input, int output, decimal? cost, string operation = "chat") =>
        ScalarAsync(
            "insert into ai_usage (id, user_id, occurred_at, correlation_id, mode, operation, model, input_tokens, output_tokens, cost_usd) " +
            "values (gen_random_uuid(), @u, @at::timestamptz, @c, @m, @o, 'modelo', @i, @out, @cost) returning 1",
            ("u", userId), ("at", occurredAt), ("c", correlation), ("m", mode), ("o", operation), ("i", input), ("out", output),
            ("cost", cost is null ? DBNull.Value : cost.Value));

    private static async Task<JsonElement> ReportAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/admin/usage{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    private static void AssertNumbers(JsonElement e, int calls, int actions, long input, long output, decimal cost)
    {
        Assert.Equal(calls, e.GetProperty("calls").GetInt32());
        Assert.Equal(actions, e.GetProperty("actions").GetInt32());
        Assert.Equal(input, e.GetProperty("inputTokens").GetInt64());
        Assert.Equal(output, e.GetProperty("outputTokens").GetInt64());
        Assert.Equal(cost, e.GetProperty("costUsd").GetDecimal());
    }

    private static List<string> Names(JsonElement e) => e.EnumerateObject().Select(p => p.Name).Order().ToList();

    // C17
    [Fact]
    public async Task Non_admin_gets_403_problem()
    {
        var client = await NewUserClientAsync();

        var response = await client.GetAsync("/api/admin/usage");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(403, (await JsonAsync(response)).GetProperty("status").GetInt32());
    }

    // C19
    [Fact]
    public async Task Totals_count_only_the_period()
    {
        var admin = await NewAdminClientAsync();
        var (_, _, userId) = await NewUserAsync();
        await InsertAsync(userId, "2001-03-09T23:59:59Z", "fora-antes", "ask", 1000, 1000, 9m);
        await InsertAsync(userId, "2001-03-10T00:00:00Z", "a1", "ask", 10, 1, 0.1m);
        await InsertAsync(userId, "2001-03-12T23:59:59Z", "a2", "ask", 20, 2, 0.2m);
        await InsertAsync(userId, "2001-03-13T00:00:00Z", "fora-depois", "ask", 1000, 1000, 9m);

        var report = await ReportAsync(admin, "?from=2001-03-10&to=2001-03-12");

        Assert.Equal("2001-03-10", report.GetProperty("from").GetString());
        Assert.Equal("2001-03-12", report.GetProperty("to").GetString());
        AssertNumbers(report.GetProperty("totals"), calls: 2, actions: 2, input: 30, output: 3, cost: 0.3m);
        Assert.Equal(0, report.GetProperty("totals").GetProperty("unpricedCalls").GetInt32());
    }

    // C20
    [Fact]
    public async Task By_user_sorted_by_cost_then_tokens()
    {
        var admin = await NewAdminClientAsync();
        var (_, cheapEmail, cheap) = await NewUserAsync();
        var (_, dearEmail, dear) = await NewUserAsync();
        var (_, manyEmail, manyTokens) = await NewUserAsync();
        var (_, fewEmail, fewTokens) = await NewUserAsync();
        await InsertAsync(fewTokens, "2001-04-10T10:00:00Z", "f1", "ask", 40, 10, null);
        await InsertAsync(cheap, "2001-04-10T10:00:00Z", "c1", "ask", 5, 5, 0.5m);
        await InsertAsync(manyTokens, "2001-04-10T10:00:00Z", "m1", "ask", 90, 10, null);
        await InsertAsync(dear, "2001-04-10T10:00:00Z", "d1", "routing", 2, 1, 0.4m, "choice");
        await InsertAsync(dear, "2001-04-10T10:00:01Z", "d1", "routing", 1, 1, 0.6m);

        var byUser = (await ReportAsync(admin, "?from=2001-04-01&to=2001-04-30")).GetProperty("byUser").EnumerateArray().ToList();

        Assert.Equal([dearEmail, cheapEmail, manyEmail, fewEmail], byUser.Select(u => u.GetProperty("email").GetString()));
        Assert.Equal(["actions", "calls", "costUsd", "email", "inputTokens", "outputTokens", "userId"], Names(byUser[0]));
        Assert.Equal(dear, byUser[0].GetProperty("userId").GetString());
        AssertNumbers(byUser[0], calls: 2, actions: 1, input: 3, output: 2, cost: 1.0m);
        AssertNumbers(byUser[2], calls: 1, actions: 1, input: 90, output: 10, cost: 0m);
    }

    // C21
    [Fact]
    public async Task By_mode_lists_all_modes_by_actions()
    {
        var admin = await NewAdminClientAsync();
        var (_, _, userId) = await NewUserAsync();
        for (var action = 0; action < 3; action++)
            foreach (var operation in new[] { "choice", "embedding", "chat" })
                await InsertAsync(userId, "2001-05-10T10:00:00Z", $"r{action}", "routing", 1, 1, 0.01m, operation);
        for (var action = 0; action < 4; action++)
            foreach (var operation in new[] { "embedding", "chat" })
                await InsertAsync(userId, "2001-05-10T10:00:00Z", $"a{action}", "ask", 2, 1, 0.02m, operation);

        var byMode = (await ReportAsync(admin, "?from=2001-05-01&to=2001-05-31")).GetProperty("byMode").EnumerateArray().ToList();

        Assert.Equal(5, byMode.Count);
        Assert.Equal(["ask", "routing"], byMode.Take(2).Select(m => m.GetProperty("mode").GetString()));
        Assert.Equal(["gap", "study", "upload"], byMode.Skip(2).Select(m => m.GetProperty("mode").GetString()!).Order());
        Assert.Equal(["actions", "calls", "costUsd", "inputTokens", "mode", "outputTokens"], Names(byMode[0]));
        AssertNumbers(byMode[0], calls: 8, actions: 4, input: 16, output: 8, cost: 0.16m);
        AssertNumbers(byMode[1], calls: 9, actions: 3, input: 9, output: 9, cost: 0.09m);
        foreach (var empty in byMode.Skip(2))
            AssertNumbers(empty, calls: 0, actions: 0, input: 0, output: 0, cost: 0m);
    }

    // C22
    [Fact]
    public async Task Defaults_to_last_30_days_and_since()
    {
        var admin = await NewAdminClientAsync();
        var (_, _, userId) = await NewUserAsync();
        await InsertAsync(userId, "2001-01-15T08:00:00Z", "antigo", "ask", 1, 1, null);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var report = await ReportAsync(admin, "");

        Assert.Equal(today.ToString("yyyy-MM-dd"), report.GetProperty("to").GetString());
        Assert.Equal(today.AddDays(-29).ToString("yyyy-MM-dd"), report.GetProperty("from").GetString());
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select min(occurred_at) from ai_usage", connection);
        var first = new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync())!);
        Assert.Equal(first, report.GetProperty("since").GetDateTimeOffset());
    }

    // C23
    [Fact]
    public async Task Since_is_null_without_usage()
    {
        var database = "usage_empty_" + Guid.NewGuid().ToString("N");
        await ScalarAsync($"create database {database}");
        var connectionString = new NpgsqlConnectionStringBuilder(Factory.ConnectionString) { Database = database }.ConnectionString;
        await using var empty = Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", connectionString));
        var client = empty.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Admin" })).EnsureSuccessStatusCode();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails:0"] = email }).Build();
        await using (var scope = empty.Services.CreateAsyncScope())
            await AdminRoleSeed.SeedAsync(scope.ServiceProvider, configuration, includeDevAccount: false, NullLogger.Instance);
        await LoginAsync(client, email);

        var report = await ReportAsync(client, "");

        Assert.Equal(JsonValueKind.Null, report.GetProperty("since").ValueKind);
        AssertNumbers(report.GetProperty("totals"), calls: 0, actions: 0, input: 0, output: 0, cost: 0m);
        Assert.Equal(0, report.GetProperty("byUser").GetArrayLength());
    }

    // C24
    [Fact]
    public async Task Invalid_period_returns_400()
    {
        var admin = await NewAdminClientAsync();

        foreach (var (query, field) in new[]
        {
            ("?from=2001-03-12&to=2001-03-10", "from"),
            ("?from=abc&to=2001-03-10", "from"),
            ("?from=2001-02-01&to=2001-02-30", "to"),
            ("?from=2001-01-01&to=2002-01-02", "to"),
        })
        {
            var response = await admin.GetAsync($"/api/admin/usage{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var errors = (await JsonAsync(response)).GetProperty("errors");
            Assert.Equal([field], errors.EnumerateObject().Select(p => p.Name));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/usage?from=2001-01-01&to=2002-01-01")).StatusCode);
    }

    // C25
    [Fact]
    public async Task Unpriced_calls_are_counted_not_summed()
    {
        var admin = await NewAdminClientAsync();
        var (_, _, userId) = await NewUserAsync();
        await InsertAsync(userId, "2001-06-10T10:00:00Z", "p1", "ask", 1, 1, 0.5m);
        await InsertAsync(userId, "2001-06-10T10:00:00Z", "p2", "ask", 1, 1, 0.25m);
        for (var i = 0; i < 3; i++)
            await InsertAsync(userId, "2001-06-10T10:00:00Z", $"s{i}", "routing", 1, 1, null, "choice");

        var totals = (await ReportAsync(admin, "?from=2001-06-01&to=2001-06-30")).GetProperty("totals");

        Assert.Equal(0.75m, totals.GetProperty("costUsd").GetDecimal());
        Assert.Equal(3, totals.GetProperty("unpricedCalls").GetInt32());
        Assert.Equal(5, totals.GetProperty("calls").GetInt32());
    }

    // C35
    [Fact]
    public async Task Response_has_surface_fields()
    {
        var admin = await NewAdminClientAsync();

        var report = await ReportAsync(admin, "?from=2001-07-01&to=2001-07-31");

        Assert.Equal(["byMode", "byUser", "from", "since", "to", "totals"], Names(report));
        Assert.Equal(["actions", "calls", "costUsd", "inputTokens", "outputTokens", "unpricedCalls"], Names(report.GetProperty("totals")));
    }
}
