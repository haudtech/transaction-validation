using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Core.Exceptions;

namespace TransactionValidation.Configuration.Middleware;

/// <summary>
/// Converts domain and upstream exceptions into RFC 7807 ProblemDetails responses so clients receive consistent 400/401/404/408/409/500/503 payloads.
/// This central exception mapping supports the API contract and error-handling guidance in the design documentation.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Converts an exception into a structured ProblemDetails response that matches the documented API error semantics.
    /// </summary>
    /// <param name="httpContext">The active HTTP request context.</param>
    /// <param name="exception">The exception raised by the application or upstream dependency.</param>
    /// <param name="cancellationToken">Token that can cancel the response write.</param>
    /// <returns>True when the exception was handled and a response was written.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            BadRequestException badRequest => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = badRequest.Message,
                Type = "https://httpstatuses.com/400"
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = notFound.Message,
                Type = "https://httpstatuses.com/404"
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflict.Message,
                Type = "https://httpstatuses.com/409"
            },
            UnauthorizedAccessException unauthorized => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = unauthorized.Message,
                Type = "https://httpstatuses.com/401"
            },
            UpstreamTimeoutException upstreamTimeout => new ProblemDetails
            {
                Status = StatusCodes.Status408RequestTimeout,
                Title = "Request Timeout",
                Detail = upstreamTimeout.Message,
                Type = "https://httpstatuses.com/408"
            },
            UpstreamServiceUnavailableException upstreamUnavailable => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service Unavailable",
                Detail = upstreamUnavailable.Message,
                Type = "https://httpstatuses.com/503"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred.",
                Type = "https://httpstatuses.com/500"
            }
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var payload = JsonSerializer.Serialize(problem);
        await httpContext.Response.WriteAsync(payload, cancellationToken);
        return true;
    }
}
