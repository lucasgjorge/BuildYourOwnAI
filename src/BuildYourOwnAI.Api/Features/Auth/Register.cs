using System.ComponentModel.DataAnnotations;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BuildYourOwnAI.Api.Features.Auth;

/// <summary>
/// Replaces Identity's <c>/register</c> (user-name, door 2): same body plus <c>fullName</c>, same error shape
/// (problem details keyed by the Identity error code, e.g. <c>DuplicateUserName</c>).
/// </summary>
public static class Register
{
    public sealed record Request(string? Email, string? Password, string? FullName);

    private static readonly EmailAddressAttribute EmailAddress = new();

    // Order -1 wins over the /register that MapIdentityApi maps on the same route.
    public static void Map(RouteGroupBuilder group) => group.MapPost("/register", Handle).WithOrder(-1);

    private static async Task<Results<Ok, ValidationProblem>> Handle(Request request, UserManager<AppUser> users)
    {
        var fullName = request.FullName?.Trim() ?? "";
        if (fullName.Length is 0 or > AppUser.FullNameMaxLength)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fullName"] = [$"O nome completo deve ter entre 1 e {AppUser.FullNameMaxLength} caracteres."],
            });

        var email = request.Email;
        if (string.IsNullOrEmpty(email) || !EmailAddress.IsValid(email))
            return ValidationProblem(IdentityResult.Failed(users.ErrorDescriber.InvalidEmail(email)));

        var user = new AppUser { UserName = email, Email = email, FullName = fullName };
        var result = await users.CreateAsync(user, request.Password ?? "");
        return result.Succeeded ? TypedResults.Ok() : ValidationProblem(result);
    }

    private static ValidationProblem ValidationProblem(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
}
