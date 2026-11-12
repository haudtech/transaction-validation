using Microsoft.AspNetCore.Authorization;

using TransactionValidation.Gateway.Management.Exceptions;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.Management.Services;

namespace TransactionValidation.Gateway.Management.Endpoints;

public static class GatewayBackendCredentialEndpoints
{
    public static IEndpointRouteBuilder MapGatewayBackendCredentialEndpoints(
        this IEndpointRouteBuilder endpoints,
        ManagementOidcOptions oidcOptions)
    {
        var readPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasAdministratorIdentity(context.User) && HasScope(context.User, oidcOptions.ReadScope))
            .Build();
        var writePolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasAdministratorIdentity(context.User) && HasScope(context.User, oidcOptions.WriteScope))
            .Build();

        var group = endpoints.MapGroup("/admin/backend-credentials");
        group.MapGet("/", async (
                GatewayBackendCredentialManagementService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await service.GetCredentialsAsync(cancellationToken));
                }
                catch (BackendCredentialManagementUnavailableException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service unavailable");
                }
            })
            .RequireAuthorization(readPolicy);

        group.MapPost("/", async (
                HttpContext context,
                GatewayBackendCredentialManagementService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var issuedCredential = await service.IssueAsync(context.User, cancellationToken);
                    context.Response.Headers.CacheControl = "no-store";
                    return Results.Ok(issuedCredential);
                }
                catch (BackendCredentialManagementUnavailableException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service unavailable");
                }
                catch (IOException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Secret storage unavailable");
                }
            })
            .RequireAuthorization(writePolicy);

        group.MapDelete("/{keyId}", async (
                string keyId,
                HttpContext context,
                GatewayBackendCredentialManagementService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var revoked = await service.RevokeAsync(keyId, context.User, cancellationToken);
                    return revoked ? Results.NoContent() : Results.NotFound();
                }
                catch (BackendCredentialManagementUnavailableException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service unavailable");
                }
                catch (IOException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Secret storage unavailable");
                }
            })
            .RequireAuthorization(writePolicy);

        return endpoints;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string requiredScope) =>
        user.FindAll("scope")
            .Concat(user.FindAll("scp"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);

    private static bool HasAdministratorIdentity(System.Security.Claims.ClaimsPrincipal user) =>
        !string.IsNullOrWhiteSpace(user.FindFirst("iss")?.Value)
        && !string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value);
}
