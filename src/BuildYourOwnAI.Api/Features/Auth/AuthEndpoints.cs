using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace BuildYourOwnAI.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        // user-name door 2: our /register (with fullName) shadows Identity's on the same route.
        Register.Map(auth);
        GetMe.Map(auth);

        // Door 4: login (?useCookies=true), manage/info, ... come from Identity.
        auth.MapIdentityApi<AppUser>();

        // Identity does not ship a logout; signing out expires the session cookie. No session is not an error.
        auth.MapPost("/logout", async (SignInManager<AppUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.NoContent();
        });

        return app;
    }
}
