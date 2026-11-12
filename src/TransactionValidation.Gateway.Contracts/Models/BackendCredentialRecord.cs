namespace TransactionValidation.Gateway.Contracts.Models;

public sealed record BackendCredentialRecord(
    string KeyId,
    string ApiId,
    byte[] Verifier,
    string PepperVersion,
    GatewayClientKeyStatus Status,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? PreviousSince);

public sealed record BackendCredentialSecretReference(string KeyId, string SecretName);
