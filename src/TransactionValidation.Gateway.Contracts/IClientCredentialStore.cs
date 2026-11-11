namespace TransactionValidation.Gateway.Contracts;

public interface IClientCredentialStore
{
    Task<ClientCredentialRecord?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken);
}

public sealed record ClientCredentialRecord(
    string KeyId,
    string ClientId,
    byte[] Verifier,
    string PepperVersion,
    GatewayClientKeyStatus Status,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    bool ClientEnabled,
    IReadOnlyList<ClientApiGrant> ApiGrants);

public sealed record ClientApiGrant(string ApiId, string ApiVersion);

public enum GatewayClientKeyStatus
{
    Current,
    Previous,
    Revoked
}
