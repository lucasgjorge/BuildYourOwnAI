using System.Net;
using System.Net.Http.Json;
using BuildYourOwnAI.Api.Tests.Infrastructure;

namespace BuildYourOwnAI.Api.Tests.Features;

public sealed class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string SessionCookie = ".AspNetCore.Identity.Application=";

    private static IEnumerable<string> SetCookies(HttpResponseMessage r) =>
        r.Headers.TryGetValues("Set-Cookie", out var v) ? v : [];

    // C1
    [Fact]
    public async Task Register_valid_returns_200_and_enables_login()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // C2
    [Fact]
    public async Task Register_duplicate_email_returns_400_DuplicateUserName()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.True((await JsonAsync(second)).GetProperty("errors").TryGetProperty("DuplicateUserName", out _));
    }

    // C3
    [Fact]
    public async Task Login_valid_sets_httponly_secure_strict_cookie()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(SetCookies(login), c => c.StartsWith(SessionCookie));
        var attributes = cookie.ToLowerInvariant();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
    }

    // C4
    [Theory]
    [InlineData("wrong-password")]
    [InlineData("unknown-email")]
    public async Task Login_invalid_returns_401_without_cookie(string scenario)
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

        var body = scenario == "wrong-password"
            ? new { email, password = "Wr0ng-password!" }
            : new { email = NewEmail(), password = Password };
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", body);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain(SetCookies(login), c => c.StartsWith(SessionCookie));
    }

    // C5
    [Fact]
    public async Task Logout_returns_204_and_ends_session()
    {
        var client = await NewUserClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/organizations")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var expired = Assert.Single(SetCookies(logout), c => c.StartsWith(SessionCookie));
        Assert.Contains("expires=thu, 01 jan 1970", expired.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/organizations")).StatusCode);
    }

    public static TheoryData<string, string> ProtectedRoutes()
    {
        var id = Guid.NewGuid();
        var doc = Guid.NewGuid();
        return new()
        {
            { "POST", "/api/organizations" },
            { "GET", "/api/organizations" },
            { "GET", $"/api/organizations/{id}" },
            { "DELETE", $"/api/organizations/{id}" },
            { "POST", $"/api/organizations/{id}/documents" },
            { "GET", $"/api/organizations/{id}/documents" },
            { "DELETE", $"/api/organizations/{id}/documents/{doc}" },
            { "POST", "/api/assistants" },
            { "GET", $"/api/assistants/{id}" },
            { "POST", $"/api/assistants/{id}/ask" },
            { "POST", "/api/jev/ask" },
            { "POST", $"/api/organizations/{id}/jev/ask" },
            { "GET", "/api/gaps" },
            { "POST", $"/api/gaps/{id}/answer" },
            { "POST", $"/api/gaps/{id}/dismiss" },
        };
    }

    // C61 (rag-mvp C6 over the routes of this feature)
    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Protected_route_without_session_returns_401(string method, string path)
    {
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { name = "x", question = "x", answer = "x" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // C43
    [Fact]
    public async Task Manage_info_reflects_session()
    {
        var anonymous = Factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/auth/manage/info")).StatusCode);

        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();

        var info = await client.GetAsync("/api/auth/manage/info");

        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        var body = await JsonAsync(info);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.False(body.GetProperty("isEmailConfirmed").GetBoolean());
    }
}
