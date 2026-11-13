namespace TransactionValidation.Gateway.Options;

public sealed class GatewayRedisOptions
{
    public const string ConnectionStringName = "GatewayRedis";

    public string ConnectionString { get; set; } = string.Empty;
}
