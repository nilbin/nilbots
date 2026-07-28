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
        int? retryAfterSeconds = error.RetryAfter is TimeSpan retry
            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))
            : null;
        return Results.Json(
            new ApplicationProblemResponse(
                "about:blank",
                Title(status),
                status,
                error.Detail,
                error.Code,
                traceId,
                retryAfterSeconds),
            statusCode: status,
            contentType: "application/problem+json");
    }

    private static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        _ => "Request Failed",
    };
}
