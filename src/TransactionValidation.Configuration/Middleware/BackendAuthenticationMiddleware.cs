using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using TransactionValidation.Configuration.Options;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Configuration.Middleware;

public sealed class BackendAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public BackendAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<BackendAuthenticationOptions> backendOptions,
        IOptions<ApiKeyOptions> apiKeyOptions,
        IGatewayCredentialVerifier? gatewayCredentialVerifier = null)
    {
        if (context.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(context);
            return;
        }

        var authenticationStatus = backendOptions.Value.AuthenticationMode switch
        {
            BackendAuthenticationMode.Gateway => gatewayCredentialVerifier is null
                ? GatewayCredentialVerificationStatus.Invalid
                : await VerifyGatewayRequestAsync(context, gatewayCredentialVerifier),
            BackendAuthenticationMode.DirectApiKey => IsValidDirectApiKey(context, apiKeyOptions.Value)
                ? GatewayCredentialVerificationStatus.Valid
                : GatewayCredentialVerificationStatus.Invalid,
            _ => GatewayCredentialVerificationStatus.Invalid
        };

        if (authenticationStatus == GatewayCredentialVerificationStatus.Unavailable)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "Service unavailable" });
            return;
        }

        if (authenticationStatus != GatewayCredentialVerificationStatus.Valid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private static async Task<GatewayCredentialVerificationStatus> VerifyGatewayRequestAsync(
        HttpContext context,
        IGatewayCredentialVerifier credentialVerifier)
    {
        if (!TryGetSingleHeader(context, GatewayAuthenticationHeaders.GatewayKey, out var credential)
            || !TryGetSingleHeader(context, GatewayAuthenticationHeaders.AuthenticatedClientId, out var clientId)
            || !AuthenticatedClientId.TryParse(clientId, out _))
        {
            return GatewayCredentialVerificationStatus.Invalid;
        }

        var result = await credentialVerifier.VerifyAsync(credential, context.RequestAborted);
        return result.Status;
    }

    private static bool IsValidDirectApiKey(HttpContext context, ApiKeyOptions options)
    {
        return TryGetSingleHeader(context, options.HeaderName, out var providedKey)
            && FixedTimeEquals(providedKey, options.ApiKey);
    }

    private static bool TryGetSingleHeader(HttpContext context, string name, out string value)
    {
        var values = context.Request.Headers[name];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            value = string.Empty;
            return false;
        }

        value = values[0]!;
        return true;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);

        try
        {
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(providedBytes);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(expectedBytes);
        }
    }
}
