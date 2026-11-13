namespace TransactionValidation.Gateway.LocalBootstrap.Options;

public sealed class LocalBootstrapOptions
{
    public const string SectionName = "LocalBootstrap";

    public string ClientId { get; set; } = "local-client";

    public string ClientKeyId { get; set; } = "local-client-key";

    public string ClientPepperVersion { get; set; } = "local-client-pepper-v1";

    public string CredentialSecretName { get; set; } = "local-client-credential";

    // Generous but finite local defaults; there is no unlimited setting.
    public int RatePerMinute { get; set; } = 600;

    public int Burst { get; set; } = 100;

    public long DailyQuota { get; set; } = 100_000;

    public long MonthlyQuota { get; set; } = 2_000_000;
}
