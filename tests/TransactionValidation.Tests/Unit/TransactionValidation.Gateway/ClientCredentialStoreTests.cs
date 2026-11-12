using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TransactionValidation.Gateway.Contracts;
using TransactionValidation.Gateway.PolicyStore;
using TransactionValidation.Gateway.PolicyStore.Entities;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class ClientCredentialStoreTests
{
    [Fact]
    public async Task GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new GatewayPolicyDbContext(options);
        dbContext.Clients.Add(new GatewayClientEntity
        {
            ClientId = "partner-1",
            Enabled = false,
            Keys =
            [
                new GatewayClientKeyEntity
                {
                    KeyId = "key-1",
                    ClientId = "partner-1",
                    Verifier = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
                    PepperVersion = "pepper-v1",
                    Status = GatewayClientKeyStatus.Current,
                    NotBefore = DateTimeOffset.UtcNow
                }
            ],
            ApiGrants =
            [
                new GatewayClientApiGrantEntity
                {
                    ClientId = "partner-1",
                    ApiId = "transaction-validation",
                    ApiVersion = "v1"
                },
                new GatewayClientApiGrantEntity
                {
                    ClientId = "partner-1",
                    ApiId = "transaction-validation",
                    ApiVersion = "v2"
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var store = new EfClientCredentialStore(dbContext);
        var result = await store.GetByKeyIdAsync("key-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("partner-1", result.ClientId);
        Assert.False(result.ClientEnabled);
        Assert.Equal("pepper-v1", result.PepperVersion);
        Assert.Equal(GatewayClientKeyStatus.Current, result.Status);
        Assert.Equal(32, result.Verifier.Length);
        Assert.Collection(
            result.ApiGrants,
            grant =>
            {
                Assert.Equal("transaction-validation", grant.ApiId);
                Assert.Equal("v1", grant.ApiVersion);
            },
            grant =>
            {
                Assert.Equal("transaction-validation", grant.ApiId);
                Assert.Equal("v2", grant.ApiVersion);
            });
    }

    [Fact]
    public async Task GetByKeyIdAsync_ReturnsNullForUnknownKey()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new GatewayPolicyDbContext(options);
        var store = new EfClientCredentialStore(dbContext);

        var result = await store.GetByKeyIdAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByKeyIdAsync_PropagatesCancellation()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new GatewayPolicyDbContext(options);
        var store = new EfClientCredentialStore(dbContext);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetByKeyIdAsync("key-1", cancellationSource.Token));
    }

    [Fact]
    public void Model_ContainsVerifierConstraintsAndMigrationWithoutRawSecrets()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var dbContext = new GatewayPolicyDbContext(options);

        var schema = dbContext.Database.GenerateCreateScript();
        var migrations = dbContext.Database.GetMigrations();
        var migrationScript = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains("octet_length", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PepperVersion", schema, StringComparison.Ordinal);
        Assert.Contains("CK_GatewayClientKeys_Revocation", schema, StringComparison.Ordinal);
        Assert.Contains("CK_GatewayClientKeys_PreviousWindow", schema, StringComparison.Ordinal);
        Assert.Contains("IX_GatewayClientKeys_ClientId_Current", schema, StringComparison.Ordinal);
        Assert.Contains("IX_GatewayClientKeys_ClientId_Previous", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("202611090001_InitialCredentialPolicy", migrations);
        Assert.Contains("202611110001_ClientKeyAuthenticationPolicy", migrations);
        Assert.Contains("TR_GatewayClientKeys_PreserveKeyId", migrationScript, StringComparison.Ordinal);
    }

    [PostgreSqlFact]
    public async Task PostgreSqlMigrationAndStore_ReadCredentialFromIsolatedSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable("GATEWAY_TEST_POSTGRES");

        var schemaName = $"gateway_test_{Guid.NewGuid():N}";
        var quotedSchemaName = new NpgsqlCommandBuilder().QuoteIdentifier(schemaName);
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync();
        await using (var createSchema = new NpgsqlCommand(
            $"CREATE SCHEMA {quotedSchemaName}",
            adminConnection))
        {
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            var schemaConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schemaName
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
                .UseNpgsql(schemaConnectionString)
                .Options;

            await using var dbContext = new GatewayPolicyDbContext(options);
            await dbContext.Database.MigrateAsync();
            dbContext.Clients.Add(new GatewayClientEntity
            {
                ClientId = "postgres-client",
                Enabled = false,
                Keys =
                [
                    new GatewayClientKeyEntity
                    {
                        KeyId = "postgres-key",
                        ClientId = "postgres-client",
                        Verifier = Enumerable.Repeat((byte)42, 32).ToArray(),
                        PepperVersion = "pepper-v1",
                        Status = GatewayClientKeyStatus.Current,
                        NotBefore = DateTimeOffset.UtcNow
                    }
                ],
                ApiGrants =
                [
                    new GatewayClientApiGrantEntity
                    {
                        ClientId = "postgres-client",
                        ApiId = "transaction-validation",
                        ApiVersion = "v1"
                    }
                ]
            });
            await dbContext.SaveChangesAsync();

            var invalidRevokedKey = new GatewayClientKeyEntity
            {
                KeyId = "revoked-without-timestamp",
                ClientId = "postgres-client",
                Verifier = Enumerable.Repeat((byte)42, 32).ToArray(),
                PepperVersion = "pepper-v1",
                Status = GatewayClientKeyStatus.Revoked,
                NotBefore = DateTimeOffset.UtcNow
            };
            dbContext.ClientKeys.Add(invalidRevokedKey);
            var invalidRevocation = await Assert.ThrowsAsync<DbUpdateException>(
                () => dbContext.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(invalidRevocation.InnerException);
            Assert.Equal("CK_GatewayClientKeys_Revocation", postgresException.ConstraintName);
            dbContext.Entry(invalidRevokedKey).State = EntityState.Detached;

            var store = new EfClientCredentialStore(dbContext);
            var result = await store.GetByKeyIdAsync("postgres-key", CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.ClientEnabled);
            Assert.Equal(32, result.Verifier.Length);
            Assert.Single(result.ApiGrants);
            Assert.Null(await store.GetByKeyIdAsync("not-found", CancellationToken.None));

            var keyId = "postgres-key";
            await Assert.ThrowsAsync<PostgresException>(
                () => dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"GatewayClientKeys\" WHERE \"KeyId\" = {keyId}"));
            var replacementKeyId = "renamed-postgres-key";
            await Assert.ThrowsAsync<PostgresException>(
                () => dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"GatewayClientKeys\" SET \"KeyId\" = {replacementKeyId} WHERE \"KeyId\" = {keyId}"));
            Assert.NotNull(await store.GetByKeyIdAsync(keyId, CancellationToken.None));

            await dbContext.Database.MigrateAsync("0");
            Assert.Empty(await dbContext.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand(
                $"DROP SCHEMA {quotedSchemaName} CASCADE",
                adminConnection);
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
