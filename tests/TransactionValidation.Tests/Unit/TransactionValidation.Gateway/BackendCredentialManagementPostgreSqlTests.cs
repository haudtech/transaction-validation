using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using TransactionValidation.Configuration.Authentication;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.Management.Services;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.PolicyStore.Stores;
using TransactionValidation.Gateway.Secrets.Providers;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class BackendCredentialManagementPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task ManagementIssuance_RotationAndRevocationAreVerifiedByApiAndAudited()
    {
        var connectionString = Environment.GetEnvironmentVariable("GATEWAY_TEST_POSTGRES");
        var schemaName = $"gateway_backend_test_{Guid.NewGuid():N}";
        var quotedSchemaName = new NpgsqlCommandBuilder().QuoteIdentifier(schemaName);
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync();
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA {quotedSchemaName}", adminConnection))
        {
            await createSchema.ExecuteNonQueryAsync();
        }

        var secretsDirectory = Path.Combine(Path.GetTempPath(), $"gateway-secrets-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(secretsDirectory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    secretsDirectory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var schemaConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schemaName
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
                .UseNpgsql(schemaConnectionString)
                .Options;
            await using var dbContext = new GatewayPolicyDbContext(options);
            await dbContext.Database.MigrateAsync();

            var secretProvider = new LocalFileSecretProvider(secretsDirectory);
            var pepper = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
            await secretProvider.WriteSecretAsync("backend-pepper-v1", pepper, CancellationToken.None);
            var managementService = new GatewayBackendCredentialManagementService(
                dbContext,
                secretProvider,
                secretProvider,
                Options.Create(new BackendCredentialManagementOptions
                {
                    PepperVersion = "backend-pepper-v1"
                }),
                TimeProvider.System);
            var administrator = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("iss", "https://issuer.example"),
                new Claim("sub", "admin-123"),
                new Claim("client_id", "management-cli")
            ],
            "test"));

            var first = await managementService.IssueAsync(administrator, CancellationToken.None);
            var credentialStore = new EfBackendCredentialStore(dbContext);
            var verifier = new DatabaseGatewayCredentialVerifier(
                credentialStore,
                secretProvider,
                TimeProvider.System);
            Assert.Equal(
                GatewayCredentialVerificationStatus.Valid,
                (await verifier.VerifyAsync(first.Credential, CancellationToken.None)).Status);

            var second = await managementService.IssueAsync(administrator, CancellationToken.None);
            var previous = await credentialStore.GetByKeyIdAsync(
                "transaction-validation",
                first.KeyId,
                CancellationToken.None);
            Assert.NotNull(previous);
            Assert.Equal(GatewayClientKeyStatus.Previous, previous.Status);
            Assert.NotNull(previous.ExpiresAt);
            Assert.True(previous.ExpiresAt <= previous.PreviousSince!.Value.AddHours(1));
            Assert.Equal(
                GatewayCredentialVerificationStatus.Valid,
                (await verifier.VerifyAsync(first.Credential, CancellationToken.None)).Status);
            Assert.Equal(second.KeyId, (await credentialStore.GetCurrentAsync(
                "transaction-validation",
                CancellationToken.None))?.KeyId);

            Assert.True(await managementService.RevokeAsync(
                second.KeyId,
                administrator,
                CancellationToken.None));
            Assert.Null(await credentialStore.GetCurrentAsync("transaction-validation", CancellationToken.None));
            Assert.Equal(3, await dbContext.AuditEvents.CountAsync());

            var auditId = await dbContext.AuditEvents.Select(audit => audit.Id).FirstAsync();
            await Assert.ThrowsAsync<PostgresException>(
                () => dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"GatewayAuditEvents\" WHERE \"Id\" = {auditId}"));
        }
        finally
        {
            if (Directory.Exists(secretsDirectory))
            {
                Directory.Delete(secretsDirectory, recursive: true);
            }

            await using var dropSchema = new NpgsqlCommand(
                $"DROP SCHEMA {quotedSchemaName} CASCADE",
                adminConnection);
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
