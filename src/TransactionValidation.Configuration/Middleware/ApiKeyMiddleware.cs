using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TransactionValidation.Configuration.Options;

namespace TransactionValidation.Configuration.Middleware;

/// <summary>
/// Validates the configured API key header before the request reaches the controller pipeline.
/// This enforces the security requirement described in the solution analysis for a partner-facing BFF.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<ApiKeyOptions> options)
    {
        var apiKeyOptions = options.Value;
        if (!apiKeyOptions.Enabled)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(apiKeyOptions.HeaderName, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey)
            || !string.Equals(providedKey.ToString(), apiKeyOptions.ApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }
}
