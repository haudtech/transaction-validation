using System.Security.Cryptography;
using System.Text;

using TransactionValidation.Configuration.Authentication;
using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class DatabaseGatewayCredentialVerifierTests
{
    private const string KeyId = "backend-key";
    private static readonly string Secret = new('A', 43);
    private static readonly byte[] Pepper = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T12:00:00Z");

    [Fact]
    public async Task VerifyAsync_WhenCredentialMatchesStoredVerifier_ReturnsValid()
    {
        var verifier = CreateVerifier(CreateRecord());

        var result = await verifier.VerifyAsync($"{KeyId}.{Secret}", CancellationToken.None);

        Assert.Equal(GatewayCredentialVerificationStatus.Valid, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WhenSecretDoesNotMatch_ReturnsInvalid()
    {
        var verifier = CreateVerifier(CreateRecord());

        var result = await verifier.VerifyAsync($"{KeyId}.{new string('B', 43)}", CancellationToken.None);

        Assert.Equal(GatewayCredentialVerificationStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WhenCredentialIsExpired_ReturnsInvalid()
    {
        var verifier = CreateVerifier(CreateRecord(expiresAt: Now.AddSeconds(-1)));

        var result = await verifier.VerifyAsync($"{KeyId}.{Secret}", CancellationToken.None);

        Assert.Equal(GatewayCredentialVerificationStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WhenPolicyStoreIsUnavailable_ReturnsUnavailable()
    {
        var verifier = new DatabaseGatewayCredentialVerifier(
            new StubBackendCredentialStore(null, fail: true),
            new StubSecretProvider(),
            new FixedTimeProvider(Now));

        var result = await verifier.VerifyAsync($"{KeyId}.{Secret}", CancellationToken.None);

        Assert.Equal(GatewayCredentialVerificationStatus.Unavailable, result.Status);
    }

    private static DatabaseGatewayCredentialVerifier CreateVerifier(BackendCredentialRecord record) =>
        new(
            new StubBackendCredentialStore(record, fail: false),
            new StubSecretProvider(),
            new FixedTimeProvider(Now));

    private static BackendCredentialRecord CreateRecord(DateTimeOffset? expiresAt = null) =>
        new(
            KeyId,
            "transaction-validation",
            HMACSHA256.HashData(Pepper, Encoding.ASCII.GetBytes(Secret)),
            "backend-pepper-v1",
            GatewayClientKeyStatus.Current,
            Now.AddMinutes(-1),
            expiresAt,
            null,
            null);

    private sealed class StubBackendCredentialStore(BackendCredentialRecord? record, bool fail)
        : IBackendCredentialStore
    {
        public Task<BackendCredentialSecretReference?> GetCurrentAsync(
            string apiId,
            CancellationToken cancellationToken) =>
            Task.FromResult<BackendCredentialSecretReference?>(null);

        public Task<BackendCredentialRecord?> GetByKeyIdAsync(
            string apiId,
            string keyId,
            CancellationToken cancellationToken)
        {
            if (fail)
            {
                throw new GatewayPolicyStoreUnavailableException("Unavailable.", new IOException());
            }

            return Task.FromResult(record);
        }
    }

    private sealed class StubSecretProvider : ISecretProvider
    {
        public ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Pepper.ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
