using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Data;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class MigrationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string RagMvpMigration = "20260924012834_InitialCreate";

    private sealed class NoUser : ICurrentUser
    {
        public string? Id => null;
    }

    // C16
    [Fact]
    public async Task Backfill_creates_one_organization_per_assistant()
    {
        var connectionString = await NewDatabaseAsync();
        await using var db = NewContext(connectionString);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(RagMvpMigration);

        var (ana, bia) = ("user-ana", "user-bia");
        var (manual, rh, vazia) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (docManual, docRh) = (Guid.NewGuid(), Guid.NewGuid());
        var created = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await ExecAsync(connectionString, $"""
            insert into "AspNetUsers" (id, email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            values ('{ana}', false, false, false, false, 0), ('{bia}', false, false, false, false, 0);
            insert into assistants (id, owner_id, name, instructions, created_at) values
              ('{manual}', '{ana}', 'Manual', null, '{created:O}'),
              ('{vazia}', '{ana}', 'Vazia', null, '{created:O}'),
              ('{rh}', '{bia}', 'RH', 'seja formal', '{created:O}');
            insert into documents (id, assistant_id, file_name, size_bytes, content_sha256, chunk_count, uploaded_at) values
              ('{docManual}', '{manual}', 'manual.txt', 10, 'sha-manual', 1, now()),
              ('{docRh}', '{rh}', 'rh.txt', 10, 'sha-rh', 2, now());
            insert into chunks (id, document_id, index, content, embedding) values
              (gen_random_uuid(), '{docManual}', 0, 'manual', array_fill(0.1, array[1536])::vector),
              (gen_random_uuid(), '{docRh}', 0, 'rh 0', array_fill(0.1, array[1536])::vector),
              (gen_random_uuid(), '{docRh}', 1, 'rh 1', array_fill(0.1, array[1536])::vector);
            """);

        await migrator.MigrateAsync();

        Assert.Equal(3, await CountAsync(connectionString, "select count(*) from organizations"));
        foreach (var (assistant, owner, name) in new[] { (manual, ana, "Manual"), (vazia, ana, "Vazia"), (rh, bia, "RH") })
        {
            Assert.Equal(1, await CountAsync(connectionString,
                $"select count(*) from organizations o join assistants a on a.organization_id = o.id where a.id = '{assistant}' and o.owner_id = '{owner}' and o.name = '{name}'"));
        }
        Assert.Equal(1, await CountAsync(connectionString,
            $"select count(*) from documents d join assistants a on a.organization_id = d.organization_id where d.id = '{docManual}' and a.id = '{manual}'"));
        Assert.Equal(1, await CountAsync(connectionString,
            $"select count(*) from documents d join assistants a on a.organization_id = d.organization_id where d.id = '{docRh}' and a.id = '{rh}'"));
        Assert.Equal(0, await CountAsync(connectionString,
            $"select count(*) from documents d join assistants a on a.organization_id = d.organization_id where a.id = '{vazia}'"));
        Assert.Equal(1, await CountAsync(connectionString, $"select count(*) from chunks where document_id = '{docManual}'"));
        Assert.Equal(2, await CountAsync(connectionString, $"select count(*) from chunks where document_id = '{docRh}'"));
    }

    // C17
    [Fact]
    public async Task Schema_has_organization_ownership_shape()
    {
        var cs = Factory.ConnectionString;

        Assert.Equal(1, await CountAsync(cs, """
            select count(*) from pg_indexes
            where tablename = 'documents' and indexdef like 'CREATE UNIQUE INDEX%(organization_id, content_sha256)%'
            """));
        Assert.Equal(0, await CountAsync(cs,
            "select count(*) from information_schema.columns where table_name = 'documents' and column_name = 'assistant_id'"));
        Assert.Equal(0, await CountAsync(cs,
            "select count(*) from information_schema.columns where table_name = 'assistants' and column_name = 'owner_id'"));
        foreach (var table in new[] { "assistants", "documents" })
        {
            Assert.Equal(1, await CountAsync(cs,
                $"select count(*) from information_schema.columns where table_name = '{table}' and column_name = 'organization_id' and is_nullable = 'NO'"));
            Assert.Equal(1, await CountAsync(cs, $"""
                select count(*) from information_schema.referential_constraints rc
                join information_schema.key_column_usage k on k.constraint_name = rc.constraint_name
                where k.table_name = '{table}' and k.column_name = 'organization_id' and rc.delete_rule = 'CASCADE'
                """));
        }
        Assert.Equal(1, await CountAsync(cs, """
            select count(*) from information_schema.columns
            where table_name = 'assistants' and column_name = 'routing_description'
              and is_nullable = 'YES' and data_type = 'character varying' and character_maximum_length = 500
            """));
    }

    // user-name C9
    [Fact]
    public async Task Schema_has_user_full_name()
    {
        Assert.Equal(1, await CountAsync(Factory.ConnectionString, """
            select count(*) from information_schema.columns
            where table_name = 'AspNetUsers' and column_name = 'full_name'
              and is_nullable = 'YES' and data_type = 'character varying' and character_maximum_length = 100
            """));
    }

    private async Task<string> NewDatabaseAsync()
    {
        var name = "mig_" + Guid.NewGuid().ToString("N");
        await ExecAsync(Factory.ConnectionString, $"create database {name}");
        return new NpgsqlConnectionStringBuilder(Factory.ConnectionString) { Database = name }.ConnectionString;
    }

    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options, new NoUser());

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
