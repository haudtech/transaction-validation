using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Validation;
using TransactionValidation.Integration;

namespace TransactionValidation.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionValidationCommonServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<PartnerVerificationOptions>(configuration.GetSection(PartnerVerificationOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
        services.Configure<SerilogOptions>(configuration.GetSection(SerilogOptions.SectionName));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ApiKeyOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PartnerVerificationOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SerilogOptions>>().Value);

        services.AddValidatorsFromAssemblyContaining<PartnerTransactionRequestValidator>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddHttpClient<IPartnerVerifier, PartnerVerifierPlaceholder>(client =>
        {
            var options = configuration.GetSection(PartnerVerificationOptions.SectionName).Get<PartnerVerificationOptions>() ?? new PartnerVerificationOptions();
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        var telemetryOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
        var exporterName = telemetryOptions.Tracing.Exporter;
        var useAspNetCoreInstrumentation = telemetryOptions.Tracing.UseAspNetCoreInstrumentation;
        var useEntityFrameworkCoreInstrumentation = telemetryOptions.Tracing.UseEntityFrameworkCoreInstrumentation;
        var azureMonitorConnectionString = !string.IsNullOrWhiteSpace(telemetryOptions.Tracing.AzureMonitor.ConnectionString)
            ? telemetryOptions.Tracing.AzureMonitor.ConnectionString
            : configuration["ApplicationInsights:ConnectionString"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("TransactionValidation"))
            .WithTracing(tracing =>
            {
                if (useAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                if (useEntityFrameworkCoreInstrumentation)
                {
                    // EF Core instrumentation is optional and currently enabled through config only.
                }

                tracing.AddHttpClientInstrumentation();

                if (string.Equals(exporterName, "Console", StringComparison.OrdinalIgnoreCase))
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();

                if (string.Equals(exporterName, "Console", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.AddConsoleExporter();
                }
            });

        if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
        {
            services.AddOpenTelemetry().UseAzureMonitor();
        }

        return services;
    }

    public static IApplicationBuilder UseTransactionValidationCommon(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<ApiKeyMiddleware>();
        return app;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(10);
    }
}
