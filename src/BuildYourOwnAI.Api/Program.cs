using System.Security.Claims;
using System.Threading.RateLimiting;
using BuildYourOwnAI.Api.Features.Ask;
using BuildYourOwnAI.Api.Features.Assistants;
using BuildYourOwnAI.Api.Features.Auth;
using BuildYourOwnAI.Api.Features.Gaps;
using BuildYourOwnAI.Api.Features.Jev;
using BuildYourOwnAI.Api.Features.Organizations;
using BuildYourOwnAI.Api.Infrastructure;
using BuildYourOwnAI.Api.Infrastructure.Ai;
using BuildYourOwnAI.Api.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql.UseVector())
    .UseSnakeCaseNamingConvention());

// Door 4: Identity endpoints with a same-origin session cookie.
// A fixed name keeps session cookies valid when the app runs from another folder (keys are isolated per content root by default).
builder.Services.AddDataProtection().SetApplicationName("BuildYourOwnAI");

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.ConfigureApplicationCookie(cookie =>
{
    cookie.Cookie.HttpOnly = true;
    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    cookie.Cookie.SameSite = SameSiteMode.Strict;
    cookie.SlidingExpiration = true;
    // An API answers 401/403; it never redirects to a login page.
    cookie.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    cookie.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy(AskPipeline.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromSeconds(60), QueueLimit = 0 }));
    limiter.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Muitas perguntas em pouco tempo. Aguarde um minuto.")
            .ExecuteAsync(context.HttpContext);
    };
});

builder.Services.AddAi(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevAdminSeed.SeedAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapOrganizationsEndpoints();
app.MapAssistantsEndpoints();
app.MapJevEndpoints();
app.MapGapsEndpoints();

// Unknown /api routes are API 404s, never the SPA page (door 9).
app.Map("/api/{**rest}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Rota não encontrada."));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
