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
        IGatewayCredentialVerifier gatewayCredentialVerifier)
    {
        if (context.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(context);
            return;
        }

        var isAuthenticated = backendOptions.Value.AuthenticationMode switch
        {
            BackendAuthenticationMode.Gateway => IsValidGatewayRequest(context, gatewayCredentialVerifier),
            BackendAuthenticationMode.DirectApiKey => IsValidDirectApiKey(context, apiKeyOptions.Value),
            _ => false
        };

        if (!isAuthenticated)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private static bool IsValidGatewayRequest(
        HttpContext context,
        IGatewayCredentialVerifier credentialVerifier)
    {
        if (!TryGetSingleHeader(context, GatewayAuthenticationHeaders.GatewayKey, out var credential)
            || !TryGetSingleHeader(context, GatewayAuthenticationHeaders.AuthenticatedClientId, out var clientId)
            || !AuthenticatedClientId.TryParse(clientId, out _))
        {
            return false;
        }

        return credentialVerifier.IsValid(credential);
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
