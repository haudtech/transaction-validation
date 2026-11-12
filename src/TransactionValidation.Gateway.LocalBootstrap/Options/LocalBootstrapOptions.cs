namespace TransactionValidation.Gateway.LocalBootstrap.Options;

public sealed class LocalBootstrapOptions
{
    public const string SectionName = "LocalBootstrap";

    public string ClientId { get; set; } = "local-client";

    public string ClientKeyId { get; set; } = "local-client-key";

    public string ClientPepperVersion { get; set; } = "local-client-pepper-v1";

    public string CredentialSecretName { get; set; } = "local-client-credential";
}
