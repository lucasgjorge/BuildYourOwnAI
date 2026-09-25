using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildYourOwnAI.Api.Infrastructure.Data;
using BuildYourOwnAI.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" });
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
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" })).EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" });

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
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" })).EnsureSuccessStatusCode();

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
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" })).EnsureSuccessStatusCode();

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
            { "GET", $"/api/organizations/{id}/documents/{doc}/chunks/0" },
            { "POST", $"/api/organizations/{id}/study-sessions" },
            { "POST", $"/api/study-sessions/{id}/questions/{doc}/answer" },
            { "POST", "/api/assistants" },
            { "GET", $"/api/assistants/{id}" },
            { "POST", $"/api/assistants/{id}/ask" },
            { "POST", "/api/route/ask" },
            { "POST", $"/api/organizations/{id}/route/ask" },
            { "GET", "/api/auth/me" },
            { "GET", "/api/gaps" },
            { "POST", $"/api/gaps/{id}/answer" },
            { "POST", $"/api/gaps/{id}/dismiss" },
            // admin-usage C18
            { "GET", "/api/admin/usage" },
        };
    }

    // C61 (rag-mvp C6 over the routes of this feature)
    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Protected_route_without_session_returns_401(string method, string path)
    {
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { name = "x", question = "x", answer = "x", questionCount = 5, option = 0 });

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
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Usuário de Teste" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();

        var info = await client.GetAsync("/api/auth/manage/info");

        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        var body = await JsonAsync(info);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.False(body.GetProperty("isEmailConfirmed").GetBoolean());
    }

    // --- user-name ---

    private Task<long> UsersWithEmailAsync(string email) =>
        ScalarAsync("select count(*) from \"AspNetUsers\" where normalized_email = upper(@e)", ("e", email));

    private async Task<string?> FullNameAsync(string email)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select full_name from \"AspNetUsers\" where normalized_email = upper(@e)", connection);
        command.Parameters.AddWithValue("e", email);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (string?)value;
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string key)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(key, out _), $"errors.{key} missing");
    }

    // user-name C1
    [Fact]
    public async Task Register_with_full_name_saves_it_and_enables_login()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Maria da Silva" });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Equal("Maria da Silva", await FullNameAsync(email));
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // user-name C2
    [Fact]
    public async Task Register_trims_full_name()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();

        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "  Maria da Silva  " })).EnsureSuccessStatusCode();

        Assert.Equal("Maria da Silva", await FullNameAsync(email));
    }

    // user-name C3
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_without_full_name_returns_400(string? fullName)
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        object body = fullName is null ? new { email, password = Password } : new { email, password = Password, fullName };

        var register = await client.PostAsJsonAsync("/api/auth/register", body);

        await AssertValidationProblemAsync(register, "fullName");
        Assert.Equal(0, await UsersWithEmailAsync(email));
    }

    // user-name C4
    [Fact]
    public async Task Register_full_name_length_bound()
    {
        var client = Factory.CreateHttpsClient();
        var tooLong = NewEmail();
        var longest = NewEmail();

        var rejected = await client.PostAsJsonAsync("/api/auth/register", new { email = tooLong, password = Password, fullName = new string('a', 101) });
        var accepted = await client.PostAsJsonAsync("/api/auth/register", new { email = longest, password = Password, fullName = new string('b', 100) });

        await AssertValidationProblemAsync(rejected, "fullName");
        Assert.Equal(0, await UsersWithEmailAsync(tooLong));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(new string('b', 100), await FullNameAsync(longest));
    }

    // user-name C6
    [Fact]
    public async Task Register_weak_password_returns_400_with_identity_code()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "abc", fullName = "Maria da Silva" });

        await AssertValidationProblemAsync(register, "PasswordTooShort");
        Assert.Equal(0, await UsersWithEmailAsync(email));
    }

    // user-name C7
    [Fact]
    public async Task Register_invalid_email_returns_400_InvalidEmail()
    {
        var client = Factory.CreateHttpsClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = "nao-e-email", password = Password, fullName = "Maria da Silva" });

        await AssertValidationProblemAsync(register, "InvalidEmail");
        Assert.Equal(0, await UsersWithEmailAsync("nao-e-email"));
    }

    // user-name C10
    [Fact]
    public async Task Me_returns_email_and_full_name()
    {
        var client = Factory.CreateHttpsClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Maria da Silva" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await JsonAsync(me);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Maria da Silva", body.GetProperty("fullName").GetString());
    }

    // user-name C12
    [Fact]
    public async Task Me_returns_null_full_name_for_account_without_name()
    {
        var email = NewEmail();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.CreateAsync(new AppUser { UserName = email, Email = email }, Password)).Succeeded);
        }
        var client = Factory.CreateHttpsClient();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = Password })).EnsureSuccessStatusCode();

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await JsonAsync(me);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("fullName").ValueKind);
    }
}
