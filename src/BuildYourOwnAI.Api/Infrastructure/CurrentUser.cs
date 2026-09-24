using System.Security.Claims;

namespace BuildYourOwnAI.Api.Infrastructure;

public interface ICurrentUser
{
    string? Id { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? Id => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
