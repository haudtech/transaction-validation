namespace TransactionValidation.Configuration.Options;

public sealed class BackendAuthenticationOptions
{
    public const string SectionName = "Security";

    public BackendAuthenticationMode AuthenticationMode { get; set; } = BackendAuthenticationMode.Gateway;

    public string GatewayKey { get; set; } = string.Empty;
}
