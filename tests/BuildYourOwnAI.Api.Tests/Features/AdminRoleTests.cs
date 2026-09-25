using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class AdminRoleTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<bool> IsAdminAsync(HttpClient client)
    {
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        return (await JsonAsync(me)).GetProperty("isAdmin").GetBoolean();
    }

    private Task<long> AdminRoleCountAsync(string email) => ScalarAsync("""
        select count(*) from "AspNetUserRoles" ur
        join "AspNetUsers" u on u.id = ur.user_id join "AspNetRoles" r on r.id = ur.role_id
        where u.normalized_email = upper(@e) and r.name = 'Admin'
        """, ("e", email));

    // C13
    [Fact]
    public async Task Startup_creates_admin_role()
    {
        Factory.CreateHttpsClient(); // boots the app

        Assert.Equal(1, await ScalarAsync("select count(*) from \"AspNetRoles\" where name = 'Admin' and normalized_name = 'ADMIN'"));
    }

    // C14
    [Fact]
    public async Task Startup_grants_role_to_admin_emails()
    {
        var (_, email, _) = await NewUserAsync();
        Assert.Equal(0, await AdminRoleCountAsync(email));

        await using var restarted = Factory.WithWebHostBuilder(b => b.UseSetting("Admin:Emails:0", email));
        var client = restarted.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        await LoginAsync(client, email);

        Assert.Equal(1, await AdminRoleCountAsync(email));
        Assert.True(await IsAdminAsync(client));
    }

    // C15
    [Fact]
    public async Task Dev_account_gets_role_only_in_development()
    {
        var (_, devEmail, _) = await NewUserAsync();
        var (_, otherEmail, _) = await NewUserAsync();

        async Task SeedAsync(string email, bool development)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DevSeed:AdminEmail"] = email })
                .Build();
            await using var scope = Factory.Services.CreateAsyncScope();
            await AdminRoleSeed.SeedAsync(scope.ServiceProvider, configuration, development, NullLogger.Instance);
        }

        await SeedAsync(otherEmail, development: false);
        await SeedAsync(devEmail, development: true);
        await SeedAsync(devEmail, development: true);

        Assert.Equal(0, await AdminRoleCountAsync(otherEmail));
        Assert.Equal(1, await AdminRoleCountAsync(devEmail));
    }

    // C16
    [Fact]
    public async Task Me_reports_is_admin()
    {
        var (client, email, _) = await NewUserAsync();
        var admin = await NewAdminClientAsync();

        var me = await JsonAsync(await client.GetAsync("/api/auth/me"));

        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Usuário de Teste", me.GetProperty("fullName").GetString());
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
        Assert.True(await IsAdminAsync(admin));
    }

    // C36
    [Fact]
    public async Task Role_applies_after_login_again()
    {
        var (client, email, _) = await NewUserAsync();

        await GrantAdminAsync(email);

        Assert.False(await IsAdminAsync(client));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/usage")).StatusCode);

        await LoginAsync(client, email);

        Assert.True(await IsAdminAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/usage")).StatusCode);
    }
}
