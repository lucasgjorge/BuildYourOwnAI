using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

/// <summary>The contract the mobile app depends on: Identity's bearer tokens (mobile-app, door 2; AD-016).</summary>
public sealed class MobileAuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<(HttpClient Client, string Email)> RegisteredAsync()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" })).EnsureSuccessStatusCode();
        return (client, email);
    }

    // C1
    [Fact]
    public async Task Login_without_cookies_returns_tokens()
    {
        var (client, email) = await RegisteredAsync();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(login.Headers.Contains("Set-Cookie"));
        var body = await JsonAsync(login);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.NotEmpty(body.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(body.GetProperty("refreshToken").GetString()!);
        Assert.True(body.GetProperty("expiresIn").GetInt64() > 0);
    }

    // C48
    [Theory]
    [InlineData("wrong-password")]
    [InlineData("unknown-email")]
    public async Task Token_login_with_wrong_credentials_returns_401(string scenario)
    {
        var (client, email) = await RegisteredAsync();
        var body = scenario == "wrong-password"
            ? new { email, password = "Wr0ng-password!" }
            : new { email = NewEmail(), password = Password };

        var login = await client.PostAsJsonAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain("accessToken", await login.Content.ReadAsStringAsync());
    }
}
