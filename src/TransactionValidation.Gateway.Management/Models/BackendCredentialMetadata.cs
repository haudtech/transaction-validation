using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Management.Models;

public sealed record BackendCredentialMetadata(
    string KeyId,
    GatewayClientKeyStatus Status,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record IssuedBackendCredential(
    string KeyId,
    string Credential,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt);
