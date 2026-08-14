using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Validation;
using TransactionValidation.Integration;
using TransactionValidation.Messaging;

namespace TransactionValidation.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionValidationCommonServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.SectionName));
        services.Configure<PartnerVerificationOptions>(configuration.GetSection(PartnerVerificationOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
        services.Configure<SerilogOptions>(configuration.GetSection(SerilogOptions.SectionName));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ApiKeyOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<IdempotencyOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PartnerVerificationOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SerilogOptions>>().Value);

        services.AddValidatorsFromAssemblyContaining<PartnerTransactionRequestValidator>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        var partnerVerificationOptions = configuration.GetSection(PartnerVerificationOptions.SectionName).Get<PartnerVerificationOptions>() ?? new PartnerVerificationOptions();

        var fallbackTimeoutSeconds = Math.Max(1, partnerVerificationOptions.TimeoutSeconds);
        var attemptTimeoutSeconds = partnerVerificationOptions.AttemptTimeoutSeconds > 0
            ? partnerVerificationOptions.AttemptTimeoutSeconds
            : fallbackTimeoutSeconds;
        var totalTimeoutSeconds = partnerVerificationOptions.TotalRequestTimeoutSeconds > 0
            ? partnerVerificationOptions.TotalRequestTimeoutSeconds
            : Math.Max(attemptTimeoutSeconds + 1, attemptTimeoutSeconds * (partnerVerificationOptions.RetryCount + 1));

        if (totalTimeoutSeconds <= attemptTimeoutSeconds)
        {
            totalTimeoutSeconds = attemptTimeoutSeconds + 1;
        }

        services.AddHttpClient<IPartnerVerifier, PartnerVerifierClient>(client =>
        {
            client.BaseAddress = new Uri(partnerVerificationOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(totalTimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = partnerVerificationOptions.RetryCount;

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptTimeoutSeconds);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(totalTimeoutSeconds);

            options.CircuitBreaker.MinimumThroughput = Math.Max(2, partnerVerificationOptions.CircuitBreakerFailures);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(partnerVerificationOptions.CircuitBreakerDurationSeconds);
        });

        services.AddSingleton<IRabbitMqClientAdapter>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            return new RabbitMqClientAdapter(options.HostName, options.Port, options.UserName, options.Password);
        });

        services.AddSingleton<IMessagePublisher>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            var rabbitMqClientAdapter = sp.GetRequiredService<IRabbitMqClientAdapter>();
            return new RabbitMqMessagePublisher(options.QueueName, options.Durable, rabbitMqClientAdapter);
        });

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

}
