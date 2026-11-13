using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Middleware;

public sealed class GatewayAuthorizationMiddleware(
    RequestDelegate next,
    ISecretProvider secretProvider)
{
    private const string TransactionPath = "/transactions/v1/partner/transactions";
    private const string ApiId = "transaction-validation";
    private const string ApiVersion = "v1";
    private const string AllowHeaderName = "Allow";
    private static readonly HashSet<string> AllowedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept",
        "Accept-Encoding",
        "Content-Length",
        "Content-Type",
        "Correlation-Id",
        "Idempotency-Key",
        "User-Agent"
    };

    // Scoped services must be injected per request; middleware is constructed once.
    public async Task InvokeAsync(
        HttpContext context,
        IBackendCredentialStore backendCredentialStore,
        IClientRequestLimiter requestLimiter)
    {
        if (context.Request.Path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!GatewayAuthenticationMiddleware.TryGetCredential(context, out var credential))
        {
            throw new InvalidOperationException("Gateway authorization requires an authenticated client.");
        }

        if (!context.Request.Path.Equals(TransactionPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!credential.ApiGrants.Any(grant =>
                string.Equals(grant.ApiId, ApiId, StringComparison.Ordinal)
                && string.Equals(grant.ApiVersion, ApiVersion, StringComparison.Ordinal)))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "Forbidden");
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers[AllowHeaderName] = HttpMethods.Post;
            return;
        }

        var limit = credential.ApiLimits.FirstOrDefault(
            candidate => string.Equals(candidate.ApiId, ApiId, StringComparison.Ordinal));
        if (limit is null)
        {
            // Explicit finite limits are mandatory; a missing policy never means unlimited.
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
            return;
        }

        RequestLimitDecision decision;
        try
        {
            decision = await requestLimiter.TryAcquireAsync(credential.ClientId, limit, context.RequestAborted);
        }
        catch (GatewayRequestLimiterUnavailableException)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
            return;
        }

        if (!decision.Allowed)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await WriteErrorAsync(
                context,
                StatusCodes.Status429TooManyRequests,
                decision.Denial == RequestLimitDenial.RateLimit ? "Rate limit exceeded" : "Quota exceeded");
            return;
        }

        string backendCredential;
        try
        {
            var credentialReference = await backendCredentialStore.GetCurrentAsync(ApiId, context.RequestAborted);
            if (credentialReference is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
                return;
            }

            var secretBytes = await secretProvider.GetSecretAsync(credentialReference.SecretName, context.RequestAborted);
            try
            {
                var secret = new UTF8Encoding(false, true).GetString(secretBytes);
                backendCredential = $"{credentialReference.KeyId}.{secret}";
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
        catch (Exception exception) when (IsSecretProviderUnavailable(exception))
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
            return;
        }

        if (!IsValidBackendCredential(backendCredential))
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
            return;
        }

        SanitizeRequestHeaders(context);
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = credential.ClientId;
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = backendCredential;
        await next(context);
    }

    private static void SanitizeRequestHeaders(HttpContext context)
    {
        foreach (var header in context.Request.Headers.Keys.ToArray())
        {
            if (!AllowedRequestHeaders.Contains(header))
            {
                context.Request.Headers.Remove(header);
            }
        }

        if (context.Items.TryGetValue(GatewayCorrelationMiddleware.CorrelationIdItemKey, out var value)
            && value is string correlationId)
        {
            context.Request.Headers["Correlation-Id"] = correlationId;
        }
    }

    private static bool IsValidBackendCredential(string credential) =>
        credential.Length is > 0 and <= 1024
        && credential.All(character => character is >= '!' and <= '~');

    private static bool IsSecretProviderUnavailable(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or PlatformNotSupportedException
            or DecoderFallbackException
            or GatewayPolicyStoreUnavailableException;

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error }, context.RequestAborted);
    }
}
