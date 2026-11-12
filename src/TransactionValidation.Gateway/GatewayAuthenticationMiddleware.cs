using Microsoft.AspNetCore.Http;

using TransactionValidation.Gateway.Contracts;

namespace TransactionValidation.Gateway;

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
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
            return;
        }

        var result = await authenticator.AuthenticateAsync(
            authorization[0],
            context.RequestAborted);

        if (result.Status == GatewayClientAuthenticationStatus.Invalid)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
            return;
        }

        if (result.Status == GatewayClientAuthenticationStatus.Unavailable)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable");
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

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error }, context.RequestAborted);
    }
}
