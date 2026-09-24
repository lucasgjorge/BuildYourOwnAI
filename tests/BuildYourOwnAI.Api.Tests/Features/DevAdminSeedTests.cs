using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class DevAdminSeedTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task SeedAsync(string? email, string? password)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevSeed:AdminEmail"] = email, ["DevSeed:AdminPassword"] = password })
            .Build();
        await using var scope = Factory.Services.CreateAsyncScope();
        await DevAdminSeed.SeedAsync(scope.ServiceProvider, configuration, NullLogger.Instance);
    }

    private Task<long> UsersWithEmailAsync(string email) =>
        ScalarAsync("select count(*) from \"AspNetUsers\" where normalized_email = upper(@e)", ("e", email));

    [Fact]
    public async Task Seeded_account_can_log_in_and_is_created_once()
    {
        var email = NewEmail();

        await SeedAsync(email, "Admin#2026");
        await SeedAsync(email, "Admin#2026");

        Assert.Equal(1, await UsersWithEmailAsync(email));
        var client = Factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = "Admin#2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Without_configuration_nothing_is_created()
    {
        var email = NewEmail();

        await SeedAsync(email, null);

        Assert.Equal(0, await UsersWithEmailAsync(email));
    }

    // user-name C13
    [Fact]
    public async Task Seeded_account_is_named_Administrador()
    {
        var email = NewEmail();

        await SeedAsync(email, "Admin#2026");

        Assert.Equal(1, await ScalarAsync(
            "select count(*) from \"AspNetUsers\" where normalized_email = upper(@e) and full_name = 'Administrador'", ("e", email)));
    }
}
