namespace TransactionValidation.Gateway.PolicyStore.Options;

public sealed class GatewayPolicyStoreOptions
{
    public const string SectionName = "Gateway:PolicyStore";

    public string ConnectionString { get; set; } = string.Empty;

    public int ConnectionTimeoutSeconds { get; set; } = 5;

    public int CommandTimeoutSeconds { get; set; } = 5;
}
