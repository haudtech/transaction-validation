namespace TransactionValidation.Gateway.Contracts.Models;

public sealed record ClientCredentialRecord(
    string KeyId,
    string ClientId,
    byte[] Verifier,
    string PepperVersion,
    GatewayClientKeyStatus Status,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? PreviousSince,
    bool ClientEnabled,
    IReadOnlyList<ClientApiGrant> ApiGrants);

public sealed record ClientApiGrant(string ApiId, string ApiVersion);

public enum GatewayClientKeyStatus
{
    Current,
    Previous,
    Revoked
}
