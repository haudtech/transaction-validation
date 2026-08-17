namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Telemetry configuration for traces and metrics emitted by the BFF.
/// It aligns with the observability recommendations in the architecture design and solution analysis docs.
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public TracingOptions Tracing { get; set; } = new();

    /// <summary>
    /// Specific tracing settings used to enable HTTP, ASP.NET Core, and optional Azure Monitor export.
    /// </summary>
    public sealed class TracingOptions
    {
        public const string SectionName = "Tracing";

        public bool UseAspNetCoreInstrumentation { get; set; } = true;

        public bool UseEntityFrameworkCoreInstrumentation { get; set; } = false;

        public string Exporter { get; set; } = "Console";

        public AzureMonitorOptions AzureMonitor { get; set; } = new();
    }

    /// <summary>
    /// Azure Monitor export configuration for telemetry when a connection string is supplied.
    /// </summary>
    public sealed class AzureMonitorOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
