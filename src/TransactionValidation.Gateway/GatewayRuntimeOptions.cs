namespace TransactionValidation.Gateway;

public sealed class GatewayRuntimeOptions
{
    public const string SectionName = "Gateway";

    public string SecretsDirectory { get; set; } = ".secrets";

    public string BackendCredentialSecretName { get; set; } = "transaction-validation-backend";
}
