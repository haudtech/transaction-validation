namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Settings for the upstream partner verification client, including base URL, retries, timeout budget, and circuit-breaker thresholds.
/// This directly reflects the resilience strategy described in docs/analysis/solution_analysis.md.
/// </summary>
public sealed class PartnerVerificationOptions
{
    public const string SectionName = "PartnerVerification";

    public string BaseUrl { get; set; } = "http://localhost:5002/";

    public int RetryCount { get; set; } = 3;

    // Backward-compatible legacy timeout setting used as fallback.
    public int TimeoutSeconds { get; set; } = 10;

    public int AttemptTimeoutSeconds { get; set; } = 10;

    public int TotalRequestTimeoutSeconds { get; set; } = 30;

    public int CircuitBreakerFailures { get; set; } = 5;

    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
