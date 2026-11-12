using System.Diagnostics;

using Microsoft.AspNetCore.Http;

namespace TransactionValidation.Gateway.Middleware;

public sealed class GatewayCorrelationMiddleware(RequestDelegate next)
{
    public const string CorrelationIdItemKey = "TransactionValidation.GatewayCorrelationId";
    private const string CorrelationHeaderName = "Correlation-Id";
    private const int MaximumCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationIdItemKey] = correlationId;
        Activity.Current?.SetTag("app.correlation_id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var values = context.Request.Headers[CorrelationHeaderName];
        if (values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]))
        {
            var value = values[0]!.Trim();
            if (value.Length <= MaximumCorrelationIdLength
                && !value.Any(char.IsControl))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
