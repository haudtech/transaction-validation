namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Configuration values for API-key authentication used by the partner-facing BFF.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Security";

    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string HeaderName { get; set; } = "X-API-Key";
}
