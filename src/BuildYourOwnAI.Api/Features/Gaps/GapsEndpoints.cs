namespace BuildYourOwnAI.Api.Features.Gaps;

public static class GapsEndpoints
{
    public static IEndpointRouteBuilder MapGapsEndpoints(this IEndpointRouteBuilder app)
    {
        var gaps = app.MapGroup("/api/gaps").RequireAuthorization().WithTags("Gaps");

        ListGaps.Map(gaps);
        AnswerGap.Map(gaps);
        DismissGap.Map(gaps);

        return app;
    }

    internal static IResult AlreadyClosed() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Esta lacuna já foi respondida ou dispensada.");
}
