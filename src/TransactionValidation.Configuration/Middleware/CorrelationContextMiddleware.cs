using System.Diagnostics;

using Microsoft.AspNetCore.Http;

using Serilog.Context;

namespace TransactionValidation.Configuration.Middleware;

/// <summary>
/// Establishes the request-wide business correlation context and connects it to OpenTelemetry and Serilog.
/// </summary>
public sealed class CorrelationContextMiddleware
{
    public const string CorrelationIdItemKey = "TransactionValidation.CorrelationId";

    private const string CorrelationHeaderName = "Correlation-Id";
    private const int MaxCorrelationIdLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        if (correlationId is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"{CorrelationHeaderName} header is invalid."
            });
            return;
        }

        context.Items[CorrelationIdItemKey] = correlationId;

        var traceId = Activity.Current?.TraceId.ToString();
        Activity.Current?.SetTag("app.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _next(context);
        }
    }

    private static string? ResolveCorrelationId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationHeaderName, out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue))
        {
            return string.IsNullOrWhiteSpace(context.TraceIdentifier)
                ? Guid.NewGuid().ToString("N")
                : context.TraceIdentifier;
        }

        var correlationId = headerValue.ToString().Trim();
        if (correlationId.Length > MaxCorrelationIdLength
            || correlationId.Any(char.IsControl))
        {
            return null;
        }

        return correlationId;
    }
}
