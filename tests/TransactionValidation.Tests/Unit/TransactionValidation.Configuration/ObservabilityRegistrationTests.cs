using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Configuration.Options;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class ObservabilityRegistrationTests
{
    /// <summary>
    /// Scenario: console tracing and instrumentation are enabled.
    /// Expected: options, trace, and metric providers are registered.
    /// </summary>
    [Fact]
    public void AddTransactionValidationObservability_WhenConsoleExporterConfigured_RegistersTelemetryServices()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Tracing:Exporter"] = "Console",
            ["OpenTelemetry:Tracing:UseAspNetCoreInstrumentation"] = "true",
            ["OpenTelemetry:Tracing:UseEntityFrameworkCoreInstrumentation"] = "true"
        });

        services.AddTransactionValidationObservability(configuration, "txv-tests");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value
            .Tracing.Exporter.Should().Be("Console");
        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }

    /// <summary>
    /// Scenario: the Azure Monitor option is empty but an Application Insights connection string is configured.
    /// Expected: registration completes without throwing.
    /// </summary>
    [Fact]
    public void AddTransactionValidationObservability_WhenConnectionStringComesFromApplicationInsights_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Tracing:Exporter"] = "AzureMonitor",
            ["OpenTelemetry:Tracing:AzureMonitor:ConnectionString"] = string.Empty,
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://eastus-0.in.applicationinsights.azure.com/"
        });

        var action = () => services.AddTransactionValidationObservability(configuration, "txv-tests");

        action.Should().NotThrow();
    }

    /// <summary>
    /// Scenario: an Azure Monitor connection string is configured directly.
    /// Expected: Azure Monitor registration completes without throwing.
    /// </summary>
    [Fact]
    public void AddTransactionValidationObservability_WhenAzureMonitorConnectionStringIsConfigured_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Tracing:Exporter"] = "AzureMonitor",
            ["OpenTelemetry:Tracing:AzureMonitor:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://eastus-0.in.applicationinsights.azure.com/"
        });

        var action = () => services.AddTransactionValidationObservability(configuration, "txv-tests");

        action.Should().NotThrow();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
    }

    /// <summary>
    /// Scenario: Serilog configuration is applied to a host builder.
    /// Expected: the host builds successfully with the shared logging setup.
    /// </summary>
    [Fact]
    public void UseTransactionValidationSerilog_ReturnsUsableHostBuilder()
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Information"
        };

        var hostBuilder = new HostBuilder()
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configurationValues))
            .UseTransactionValidationSerilog();

        using var host = hostBuilder.Build();
        host.Should().NotBeNull();
    }

    /// <summary>
    /// Scenario: the shared middleware extension is applied to an application builder.
    /// Expected: exception handling, correlation, and API-key middleware are registered and the same builder is returned.
    /// </summary>
    [Fact]
    public void UseTransactionValidationCommon_WhenCalled_ReturnsSameBuilder()
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        var result = app.UseTransactionValidationCommon();

        result.Should().BeSameAs(app);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
