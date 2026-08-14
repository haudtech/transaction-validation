namespace TransactionValidation.Configuration.Options;

public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    public MinimumLevelOptions MinimumLevel { get; set; } = new();

    public List<string> Enrich { get; set; } = new();

    public List<WriteToOptions> WriteTo { get; set; } = new();

    public Dictionary<string, string> Properties { get; set; } = new();

    public sealed class MinimumLevelOptions
    {
        public string Default { get; set; } = "Information";

        public OverrideOptions Override { get; set; } = new();
    }

    public sealed class OverrideOptions
    {
        public string Microsoft { get; set; } = "Information";

        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }

    public sealed class WriteToOptions
    {
        public string Name { get; set; } = "Console";

        public Dictionary<string, string> Args { get; set; } = new();
    }
}
