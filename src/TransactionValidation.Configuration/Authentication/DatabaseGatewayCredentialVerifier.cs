using System.Security.Cryptography;
using System.Text;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Configuration.Authentication;

public sealed class DatabaseGatewayCredentialVerifier(
    IBackendCredentialStore credentialStore,
    ISecretProvider secretProvider,
    TimeProvider timeProvider) : IGatewayCredentialVerifier
{
    private const string ApiId = "transaction-validation";
    private const int SecretLength = 43;
    private const int MinimumPepperLength = 32;

    public async ValueTask<GatewayCredentialVerificationResult> VerifyAsync(
        string credential,
        CancellationToken cancellationToken)
    {
        if (!TryParse(credential, out var keyId, out var secret))
        {
            return GatewayCredentialVerificationResult.Invalid;
        }

        BackendCredentialRecord? record;
        try
        {
            record = await credentialStore.GetByKeyIdAsync(ApiId, keyId, cancellationToken);
        }
        catch (GatewayPolicyStoreUnavailableException)
        {
            return GatewayCredentialVerificationResult.Unavailable;
        }

        var now = timeProvider.GetUtcNow();
        if (record is null || !IsActive(record, now))
        {
            return GatewayCredentialVerificationResult.Invalid;
        }

        byte[] pepper;
        try
        {
            pepper = await secretProvider.GetSecretAsync(record.PepperVersion, cancellationToken);
        }
        catch (Exception exception) when (IsSecretProviderUnavailable(exception))
        {
            return GatewayCredentialVerificationResult.Unavailable;
        }

        if (pepper.Length < MinimumPepperLength)
        {
            CryptographicOperations.ZeroMemory(pepper);
            return GatewayCredentialVerificationResult.Unavailable;
        }

        var secretBytes = Encoding.ASCII.GetBytes(secret);
        var verifier = HMACSHA256.HashData(pepper, secretBytes);
        try
        {
            return record.Verifier.Length == verifier.Length
                && CryptographicOperations.FixedTimeEquals(verifier, record.Verifier)
                    ? GatewayCredentialVerificationResult.Valid
                    : GatewayCredentialVerificationResult.Invalid;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pepper);
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(verifier);
        }
    }

    private static bool TryParse(string value, out string keyId, out string secret)
    {
        keyId = string.Empty;
        secret = string.Empty;
        var separator = value.IndexOf('.');
        if (separator is < 1 or > 64 || separator != value.LastIndexOf('.'))
        {
            return false;
        }

        keyId = value[..separator];
        secret = value[(separator + 1)..];
        return keyId.All(character =>
                character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-' or '_')
            && secret.Length == SecretLength
            && secret.All(character =>
                character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-' or '_');
    }

    private static bool IsActive(BackendCredentialRecord credential, DateTimeOffset now)
    {
        if (credential.RevokedAt is not null || credential.NotBefore > now)
        {
            return false;
        }

        return credential.Status switch
        {
            GatewayClientKeyStatus.Current =>
                credential.PreviousSince is null
                && (credential.ExpiresAt is null || credential.ExpiresAt > now),
            GatewayClientKeyStatus.Previous =>
                credential.PreviousSince is { } previousSince
                && credential.ExpiresAt is { } expiresAt
                && previousSince <= now
                && expiresAt > now
                && expiresAt <= previousSince.AddHours(1),
            _ => false
        };
    }

    private static bool IsSecretProviderUnavailable(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or PlatformNotSupportedException
            or ArgumentException;
}
