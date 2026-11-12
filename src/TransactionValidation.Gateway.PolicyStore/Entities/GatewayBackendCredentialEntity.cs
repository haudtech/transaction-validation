namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayBackendCredentialEntity
{
    public required string KeyId { get; set; }

    public required string ApiId { get; set; }

    public required byte[] Verifier { get; set; }

    public required string PepperVersion { get; set; }

    public required string SecretName { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset NotBefore { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? PreviousSince { get; set; }

    public int Version { get; set; }
}
