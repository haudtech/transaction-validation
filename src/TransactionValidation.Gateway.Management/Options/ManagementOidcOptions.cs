namespace TransactionValidation.Gateway.Management.Options;

public sealed class ManagementOidcOptions
{
    public const string SectionName = "Management:Oidc";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string ReadScope { get; set; } = "gateway.read";

    public string WriteScope { get; set; } = "gateway.write";
}
