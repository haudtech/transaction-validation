using System.Security.Cryptography;
using System.Text;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Authentication;

public sealed class GatewayClientAuthenticator(
    IClientCredentialStore credentialStore,
    ISecretProvider secretProvider,
    TimeProvider timeProvider)
{
    private const int ClientSecretLength = 43;
    private const int MinimumPepperLength = 32;

    public async Task<GatewayClientAuthenticationResult> AuthenticateAsync(
        string? authorization,
        CancellationToken cancellationToken)
    {
        if (!TryParseAuthorization(authorization, out var keyId, out var secret))
        {
            return GatewayClientAuthenticationResult.Invalid;
        }

        ClientCredentialRecord? credential;
        try
        {
            credential = await credentialStore.GetByKeyIdAsync(keyId, cancellationToken);
        }
        catch (GatewayPolicyStoreUnavailableException)
        {
            return GatewayClientAuthenticationResult.Unavailable;
        }

        if (credential is null || !IsCredentialActive(credential, timeProvider.GetUtcNow()))
        {
            return GatewayClientAuthenticationResult.Invalid;
        }

        byte[] pepper;
        try
        {
            pepper = await secretProvider.GetSecretAsync(credential.PepperVersion, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return GatewayClientAuthenticationResult.Invalid;
        }
        catch (ArgumentException)
        {
            return GatewayClientAuthenticationResult.Invalid;
        }
        catch (Exception exception) when (IsSecretProviderUnavailable(exception))
        {
            return GatewayClientAuthenticationResult.Unavailable;
        }

        if (pepper.Length < MinimumPepperLength)
        {
            CryptographicOperations.ZeroMemory(pepper);
            return GatewayClientAuthenticationResult.Unavailable;
        }

        var secretBytes = Encoding.ASCII.GetBytes(secret);
        var verifier = HMACSHA256.HashData(pepper, secretBytes);
        try
        {
            if (credential.Verifier.Length != verifier.Length
                || !CryptographicOperations.FixedTimeEquals(verifier, credential.Verifier))
            {
                return GatewayClientAuthenticationResult.Invalid;
            }

            if (!AuthenticatedClientId.TryParse(credential.ClientId, out _))
            {
                return GatewayClientAuthenticationResult.Invalid;
            }

            return GatewayClientAuthenticationResult.Authenticated(credential);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pepper);
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(verifier);
        }
    }

    private static bool TryParseAuthorization(string? authorization, out string keyId, out string secret)
    {
        keyId = string.Empty;
        secret = string.Empty;
        const string scheme = "ApiKey ";

        if (authorization is null
            || !authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var credential = authorization[scheme.Length..];
        var separator = credential.IndexOf('.');
        if (separator is < 1
            || separator != credential.LastIndexOf('.')
            || separator > 64)
        {
            return false;
        }

        keyId = credential[..separator];
        secret = credential[(separator + 1)..];
        return IsSafeKeyId(keyId) && IsClientSecret(secret);
    }

    private static bool IsSafeKeyId(string value) =>
        value.Length is > 0 and <= 64
        && IsAsciiAlphaNumeric(value[0])
        && value.All(character => IsAsciiAlphaNumeric(character) || character is '-' or '_');

    private static bool IsClientSecret(string value) =>
        value.Length == ClientSecretLength
        && value.All(character =>
            IsAsciiAlphaNumeric(character) || character is '-' or '_');

    private static bool IsAsciiAlphaNumeric(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9';

    private static bool IsCredentialActive(ClientCredentialRecord credential, DateTimeOffset now)
    {
        if (!credential.ClientEnabled
            || credential.RevokedAt is not null
            || credential.NotBefore > now)
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
            or PlatformNotSupportedException;
}

public sealed record GatewayClientAuthenticationResult(
    GatewayClientAuthenticationStatus Status,
    ClientCredentialRecord? Credential)
{
    public static GatewayClientAuthenticationResult Invalid { get; } =
        new(GatewayClientAuthenticationStatus.Invalid, null);

    public static GatewayClientAuthenticationResult Unavailable { get; } =
        new(GatewayClientAuthenticationStatus.Unavailable, null);

    public static GatewayClientAuthenticationResult Authenticated(ClientCredentialRecord credential) =>
        new(GatewayClientAuthenticationStatus.Authenticated, credential);
}

public enum GatewayClientAuthenticationStatus
{
    Invalid,
    Authenticated,
    Unavailable
}
