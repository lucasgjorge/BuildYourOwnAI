namespace BuildYourOwnAI.Api.Common;

/// <summary>Wraps a call to the AI provider so any failure becomes a 502 that never leaks the provider's message.</summary>
public static class AiProviderCall
{
    public static async Task<(T? Value, IResult? Failure)> TryAsync<T>(Func<Task<T>> call, ILogger logger, string operation)
    {
        try
        {
            return (await call(), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("AI provider call {Operation} failed with {ExceptionType}", operation, ex.GetType().Name);
            return (default, Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "O provedor de IA falhou. Tente novamente em instantes."));
        }
    }
}
