using System.Diagnostics;

namespace BotArena.App.Shared;

public static class ApplicationProblemDetails
{
    public static IResult ToProblemDetails(
        this ApplicationError error,
        HttpContext httpContext)
    {
        int status = error.Type switch
        {
            ApplicationErrorType.Authentication => StatusCodes.Status401Unauthorized,
            ApplicationErrorType.Authorization => StatusCodes.Status403Forbidden,
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorType.RateLimit => StatusCodes.Status429TooManyRequests,
            _ => throw new ArgumentOutOfRangeException(nameof(error)),
        };
        if (error.RetryAfter is TimeSpan retryAfter)
        {
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        string traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        return Results.Problem(
            detail: error.Detail,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["traceId"] = traceId,
            });
    }
}
