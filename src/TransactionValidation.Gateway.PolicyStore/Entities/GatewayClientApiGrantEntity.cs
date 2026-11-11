namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayClientApiGrantEntity
{
    public required string ClientId { get; set; }

    public required string ApiId { get; set; }

    public required string ApiVersion { get; set; }

    public GatewayClientEntity Client { get; set; } = null!;
}
