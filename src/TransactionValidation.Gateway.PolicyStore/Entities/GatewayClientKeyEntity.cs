using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayClientKeyEntity
{
    public required string KeyId { get; set; }

    public required string ClientId { get; set; }

    public required byte[] Verifier { get; set; }

    public required string PepperVersion { get; set; }

    public GatewayClientKeyStatus Status { get; set; }

    public DateTimeOffset NotBefore { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? PreviousSince { get; set; }

    public GatewayClientEntity Client { get; set; } = null!;
}
