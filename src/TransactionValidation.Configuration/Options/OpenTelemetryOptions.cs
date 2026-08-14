namespace TransactionValidation.Configuration.Options;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public TracingOptions Tracing { get; set; } = new();

    public sealed class TracingOptions
    {
        public const string SectionName = "Tracing";

        public bool UseAspNetCoreInstrumentation { get; set; } = true;

        public bool UseEntityFrameworkCoreInstrumentation { get; set; } = false;

        public string Exporter { get; set; } = "Console";

        public AzureMonitorOptions AzureMonitor { get; set; } = new();
    }

    public sealed class AzureMonitorOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
