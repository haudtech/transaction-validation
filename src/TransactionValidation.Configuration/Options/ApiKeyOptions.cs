namespace TransactionValidation.Configuration.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Security";

    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string HeaderName { get; set; } = "X-API-Key";
}
