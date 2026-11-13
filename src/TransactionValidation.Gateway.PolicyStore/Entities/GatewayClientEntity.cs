namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayClientEntity
{
    public required string ClientId { get; set; }

    public bool Enabled { get; set; }

    public ICollection<GatewayClientKeyEntity> Keys { get; set; } = [];

    public ICollection<GatewayClientApiGrantEntity> ApiGrants { get; set; } = [];

    public ICollection<GatewayClientApiLimitEntity> ApiLimits { get; set; } = [];
}
