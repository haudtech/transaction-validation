using Microsoft.AspNetCore.Http;

using TransactionValidation.Gateway.Authentication;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Errors;

namespace TransactionValidation.Gateway.Middleware;

public sealed class GatewayAuthenticationMiddleware(RequestDelegate next)
{
    private const string CredentialItemKey = "TransactionValidation.GatewayClientCredential";
    private const string AuthorizationHeaderName = "Authorization";

    public async Task InvokeAsync(HttpContext context, GatewayClientAuthenticator authenticator)
    {
        if (context.Request.Path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var authorization = context.Request.Headers[AuthorizationHeaderName];
        if (authorization.Count != 1)
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, GatewayErrorCodes.Unauthorized, "Authentication failed.");
            return;
        }

        var result = await authenticator.AuthenticateAsync(
            authorization[0],
            context.RequestAborted);

        if (result.Status == GatewayClientAuthenticationStatus.Invalid)
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, GatewayErrorCodes.Unauthorized, "Authentication failed.");
            return;
        }

        if (result.Status == GatewayClientAuthenticationStatus.Unavailable)
        {
            await GatewayProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, GatewayErrorCodes.PolicyUnavailable, "Service unavailable.");
            return;
        }

        context.Items[CredentialItemKey] = result.Credential!;
        await next(context);
    }

    internal static bool TryGetCredential(HttpContext context, out ClientCredentialRecord credential)
    {
        if (context.Items.TryGetValue(CredentialItemKey, out var value)
            && value is ClientCredentialRecord found)
        {
            credential = found;
            return true;
        }

        credential = null!;
        return false;
    }

}
