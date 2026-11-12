using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.PolicyStore.Entities;
using TransactionValidation.Gateway.PolicyStore.Stores;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class BackendCredentialStoreTests
{
    [Fact]
    public async Task GetCurrentAsync_ReturnsOnlyTheActiveCurrentCredentialReference()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new GatewayPolicyDbContext(options);
        var now = DateTimeOffset.UtcNow;
        dbContext.BackendCredentials.AddRange(
            CreateCredential("current", GatewayClientKeyStatus.Current, now.AddMinutes(-1)),
            CreateCredential("previous", GatewayClientKeyStatus.Previous, now.AddHours(-2)),
            CreateCredential("expired", GatewayClientKeyStatus.Current, now.AddHours(-2), now.AddSeconds(-1)));
        await dbContext.SaveChangesAsync();

        var store = new EfBackendCredentialStore(dbContext);

        var result = await store.GetCurrentAsync("transaction-validation", CancellationToken.None);

        Assert.Equal("current", result?.KeyId);
        Assert.Equal("secret-current", result?.SecretName);
    }

    [Fact]
    public async Task GetByKeyIdAsync_ReturnsVerifierAndLifecycleMetadata()
    {
        var options = new DbContextOptionsBuilder<GatewayPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new GatewayPolicyDbContext(options);
        var credential = CreateCredential("backend-1", GatewayClientKeyStatus.Previous, DateTimeOffset.UtcNow.AddMinutes(-2));
        credential.PreviousSince = DateTimeOffset.UtcNow.AddMinutes(-1);
        credential.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(59);
        dbContext.BackendCredentials.Add(credential);
        await dbContext.SaveChangesAsync();

        var result = await new EfBackendCredentialStore(dbContext)
            .GetByKeyIdAsync("transaction-validation", "backend-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GatewayClientKeyStatus.Previous, result.Status);
        Assert.Equal("pepper-v1", result.PepperVersion);
        Assert.Equal(32, result.Verifier.Length);
        Assert.NotNull(result.PreviousSince);
    }

    private static GatewayBackendCredentialEntity CreateCredential(
        string keyId,
        GatewayClientKeyStatus status,
        DateTimeOffset notBefore,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            KeyId = keyId,
            ApiId = "transaction-validation",
            Verifier = new byte[32],
            PepperVersion = "pepper-v1",
            SecretName = $"secret-{keyId}",
            Status = status.ToString(),
            NotBefore = notBefore,
            ExpiresAt = expiresAt,
            Version = 1
        };
}
