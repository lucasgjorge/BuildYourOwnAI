using BuildYourOwnAI.Api.Infrastructure.Data;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class UsageSchemaTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<string?> StringAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    private async Task<string?> InsertErrorAsync(string userId, string mode, string operation)
    {
        try
        {
            await ScalarAsync(
                "insert into ai_usage (id, user_id, occurred_at, correlation_id, mode, operation, model, input_tokens, output_tokens) " +
                "values (gen_random_uuid(), @u, now(), 'c', @m, @o, 'x', 0, 0) returning 1",
                ("u", userId), ("m", mode), ("o", operation));
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.ConstraintName;
        }
    }

    // C10
    [Fact]
    public async Task Schema_has_ai_usage()
    {
        var (_, _, userId) = await NewUserAsync();

        Assert.Equal(
            "correlation_id:character varying:64:NO|cost_usd:numeric::YES|input_tokens:integer::NO|mode:character varying:16:NO|" +
            "model:character varying:100:NO|occurred_at:timestamp with time zone::NO|operation:character varying:16:NO|" +
            "output_tokens:integer::NO|user_id:text::NO",
            await StringAsync("""
                select string_agg(column_name || ':' || data_type || ':' || coalesce(character_maximum_length::text, '') || ':' || is_nullable, '|' order by column_name)
                from information_schema.columns where table_name = 'ai_usage' and column_name <> 'id'
                """));
        Assert.Equal("12,6", await StringAsync(
            "select numeric_precision || ',' || numeric_scale from information_schema.columns where table_name = 'ai_usage' and column_name = 'cost_usd'"));
        Assert.Equal("c", await StringAsync("""
            select confdeltype from pg_constraint
            where conrelid = 'ai_usage'::regclass and contype = 'f' and confrelid = '"AspNetUsers"'::regclass
            """));
        var indexes = await StringAsync("select string_agg(indexdef, ' | ') from pg_indexes where tablename = 'ai_usage'");
        Assert.Contains(indexes!.Split(" | "), d => d.EndsWith("(occurred_at)"));
        Assert.Contains(indexes.Split(" | "), d => d.EndsWith("(user_id, occurred_at)"));

        Assert.Equal("ck_ai_usage_mode", await InsertErrorAsync(userId, "outro", "chat"));
        Assert.Equal("ck_ai_usage_operation", await InsertErrorAsync(userId, "ask", "outro"));
        foreach (var mode in new[] { "ask", "routing", "study", "upload", "gap" })
            foreach (var operation in new[] { "chat", "embedding", "choice" })
                Assert.Null(await InsertErrorAsync(userId, mode, operation));
    }

    // C11
    [Fact]
    public async Task Deleting_user_deletes_usage()
    {
        var (_, email, userId) = await NewUserAsync();
        var (_, _, otherId) = await NewUserAsync();
        foreach (var id in new[] { userId, otherId })
            await ScalarAsync(
                "insert into ai_usage (id, user_id, occurred_at, correlation_id, mode, operation, model, input_tokens, output_tokens) " +
                "values (gen_random_uuid(), @u, now(), 'c', 'ask', 'chat', 'x', 1, 1) returning 1", ("u", id));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.DeleteAsync((await users.FindByEmailAsync(email))!)).Succeeded);
        }

        Assert.Equal(0, await ScalarAsync("select count(*) from ai_usage where user_id = @u", ("u", userId)));
        Assert.Equal(1, await ScalarAsync("select count(*) from ai_usage where user_id = @u", ("u", otherId)));
    }
}
