using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.LocalBootstrap.Options;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.Management.Services;
using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.LocalBootstrap.Services;

public sealed partial class LocalGatewayBootstrapper(
    GatewayPolicyDbContext dbContext,
    IBackendCredentialStore backendCredentialStore,
    GatewayBackendCredentialManagementService managementService,
    LocalClientSeeder clientSeeder,
    LocalSecretProvisioner provisioner,
    IOptions<BackendCredentialManagementOptions> backendOptions,
    IOptions<LocalBootstrapOptions> bootstrapOptions,
    ILogger<LocalGatewayBootstrapper> logger)
{
    private const string ApiId = "transaction-validation";
    private const string BootstrapIdentity = "local-bootstrap";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        provisioner.PrepareDirectory();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await provisioner.EnsurePepperAsync(backendOptions.Value.PepperVersion, cancellationToken);

        var clientCreated = await clientSeeder.SeedAsync(cancellationToken);
        if (clientCreated)
        {
            LogClientSeeded(bootstrapOptions.Value.ClientId, bootstrapOptions.Value.CredentialSecretName);
        }
        else
        {
            LogClientExists(bootstrapOptions.Value.ClientId, bootstrapOptions.Value.CredentialSecretName);
        }

        var current = await backendCredentialStore.GetCurrentAsync(ApiId, cancellationToken);
        if (current is not null && await provisioner.ExistsAsync(current.SecretName, cancellationToken))
        {
            LogBackendCurrent(current.KeyId);
            return;
        }

        var administrator = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", BootstrapIdentity),
            new Claim("sub", BootstrapIdentity)
        ], BootstrapIdentity));
        var issued = await managementService.IssueAsync(administrator, cancellationToken);
        LogBackendIssued(issued.KeyId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded local client '{ClientId}'; its credential is in secret '{SecretName}'.")]
    private partial void LogClientSeeded(string clientId, string secretName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local client '{ClientId}' is already seeded; credential secret is '{SecretName}'.")]
    private partial void LogClientExists(string clientId, string secretName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backend credential '{KeyId}' is current.")]
    private partial void LogBackendCurrent(string keyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Issued backend credential '{KeyId}'.")]
    private partial void LogBackendIssued(string keyId);
}
