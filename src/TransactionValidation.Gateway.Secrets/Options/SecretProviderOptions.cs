namespace TransactionValidation.Gateway.Secrets.Options;

public sealed class SecretProviderOptions
{
    public const string SectionName = "Secrets";
    public const string LocalFileProvider = "LocalFile";

    public string Provider { get; set; } = string.Empty;

    public string Directory { get; set; } = ".secrets";
}
