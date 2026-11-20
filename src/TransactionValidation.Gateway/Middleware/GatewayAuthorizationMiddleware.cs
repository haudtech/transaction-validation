using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Errors;
using TransactionValidation.Gateway.HealthChecks;

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

    public async Task InvokeAsync(HttpContext context)
    {
        if (GatewayHealthPaths.IsHealthProbe(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!GatewayAuthenticationMiddleware.TryGetCredential(context, out var credential))
        {
            throw new InvalidOperationException("Gateway authorization requires an authenticated client.");
        }

        var backendCredentialStore = context.RequestServices.GetRequiredService<IBackendCredentialStore>();
        var requestLimiter = context.RequestServices.GetRequiredService<IClientRequestLimiter>();

        if (!context.Request.Path.Equals(TransactionPath, StringComparison.OrdinalIgnoreCase))
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status404NotFound, GatewayErrorCodes.NotFound, "The requested route was not found.");
            return;
        }

        GatewayRequestMetadata.SetApi(context, ApiId, ApiVersion);

        if (!credential.ApiGrants.Any(grant =>
                string.Equals(grant.ApiId, ApiId, StringComparison.Ordinal)
                && string.Equals(grant.ApiVersion, ApiVersion, StringComparison.Ordinal)))
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, GatewayErrorCodes.Forbidden, "Access denied.");
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.Headers[AllowHeaderName] = HttpMethods.Post;
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status405MethodNotAllowed, GatewayErrorCodes.MethodNotAllowed, "The request method is not allowed.");
            return;
        }

        var limit = credential.ApiLimits.FirstOrDefault(
            candidate => string.Equals(candidate.ApiId, ApiId, StringComparison.Ordinal));
        if (limit is null)
        {
            // Explicit finite limits are mandatory; a missing policy never means unlimited.
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.PolicyUnavailable, "Service unavailable.");
            return;
        }

        RequestLimitDecision decision;
        try
        {
            decision = await requestLimiter.TryAcquireAsync(credential.ClientId, limit, context.RequestAborted);
        }
        catch (GatewayRequestLimiterUnavailableException)
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.LimiterUnavailable, "Service unavailable.");
            return;
        }

        if (!decision.Allowed)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await GatewayProblemWriter.WriteAsync(
                context,
                StatusCodes.Status429TooManyRequests,
                decision.Denial == RequestLimitDenial.RateLimit ? GatewayErrorCodes.RateLimitExceeded : GatewayErrorCodes.QuotaExceeded,
                decision.Denial == RequestLimitDenial.RateLimit ? "Rate limit exceeded" : "Quota exceeded");
            return;
        }

        string backendCredential;
        try
        {
            var credentialReference = await backendCredentialStore.GetCurrentAsync(ApiId, context.RequestAborted);
            if (credentialReference is null)
            {
                await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.PolicyUnavailable, "Service unavailable.");
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
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.PolicyUnavailable, "Service unavailable.");
            return;
        }

        if (!IsValidBackendCredential(backendCredential))
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.PolicyUnavailable, "Service unavailable.");
            return;
        }

        SanitizeRequestHeaders(context);
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = credential.ClientId;
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = backendCredential;
        GatewayRequestMetadata.SetDecision(context, GatewayRequestMetadata.Forwarded);
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
            context.Request.Headers[GatewayProblemConventions.CorrelationHeaderName] = correlationId;
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

}
