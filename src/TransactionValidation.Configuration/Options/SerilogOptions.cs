namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Structured logging configuration for the application, including minimal log level and console sinks.
/// This supports the operational observability guidance in the architecture documentation.
/// </summary>
public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    public MinimumLevelOptions MinimumLevel { get; set; } = new();

    public List<string> Enrich { get; set; } = new();

    public List<WriteToOptions> WriteTo { get; set; } = new();

    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// Default and override minimum log levels used by the application logger.
    /// </summary>
    public sealed class MinimumLevelOptions
    {
        public string Default { get; set; } = "Information";

        public OverrideOptions Override { get; set; } = new();
    }

    /// <summary>
    /// Per-namespace log-level overrides for framework and middleware noise.
    /// </summary>
    public sealed class OverrideOptions
    {
        public string Microsoft { get; set; } = "Information";

        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }

    /// <summary>
    /// Sink configuration for writing log output to a target such as the console.
    /// </summary>
    public sealed class WriteToOptions
    {
        public string Name { get; set; } = "Console";

        public Dictionary<string, string> Args { get; set; } = new();
    }
}
