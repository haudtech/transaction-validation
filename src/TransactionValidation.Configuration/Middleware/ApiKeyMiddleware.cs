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

    /// <summary>
    /// Initializes the middleware with the next delegate in the ASP.NET Core pipeline.
    /// </summary>
    /// <param name="next">The next middleware component.</param>
    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Validates the configured API-key header before allowing the request to reach the controller pipeline.
    /// </summary>
    /// <param name="context">The active HTTP request.</param>
    /// <param name="options">The configured API key settings.</param>
    public async Task InvokeAsync(HttpContext context, IOptions<ApiKeyOptions> options)
    {
        // Platform readiness/liveness probes don't send the API key header.
        if (context.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(context);
            return;
        }

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
