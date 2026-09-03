using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Serilog;

using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddTransactionValidationConfiguration(builder.Environment, args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransactionValidationCommonServices(builder.Configuration);
builder.Services.AddConfiguredBroker(
    builder.Configuration,
    AddRabbitMqMessagingServices,
    AddAzureServiceBusMessagingServices);

builder.Services.AddSingleton<IIdempotencyStore>(sp =>
{
    var options = sp.GetRequiredService<IdempotencyOptions>();
    var idempotencyWindowMinutes = Math.Clamp(options.WindowMinutes, 10, 15);
    return new InMemoryIdempotencyStore(TimeSpan.FromMinutes(idempotencyWindowMinutes));
});

var app = builder.Build();

app.UseTransactionValidationCommon();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation API"));

app.Run();

/// <summary>
/// ASP.NET Core entry point for the TransactionValidation BFF.
/// The application wires together configuration, middleware, API key enforcement, Swagger, and the API dependency graph described in the architecture and solution analysis docs.
/// See also: docs/analysis/solution_analysis.md and docs/architecture_design/Architecture_design.md.
/// </summary>
public partial class Program
{
    private static void AddRabbitMqMessagingServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value);

        services.AddSingleton<IRabbitMqClientAdapter>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            return new RabbitMqClientAdapter(
                options.HostName,
                options.Port,
                options.UserName,
                options.Password,
                options.PublishConfirmTimeoutSeconds);
        });

        services.AddSingleton<IMessageRoutingKeyResolver>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            return new PartnerTransactionRoutingKeyResolver(options.RoutingKeyPrefix);
        });

        services.AddHostedService(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            return new RabbitMqTopologyInitializer(
                options.ExchangeName,
                options.ExchangeType,
                options.Durable,
                options.AlternateExchangeName,
                options.UnroutedQueueName,
                sp.GetRequiredService<IRabbitMqClientAdapter>(),
                sp.GetRequiredService<ILogger<RabbitMqTopologyInitializer>>());
        });

        services.AddSingleton<IMessagePublisher>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMqOptions>();
            var rabbitMqClientAdapter = sp.GetRequiredService<IRabbitMqClientAdapter>();
            var routingKeyResolver = sp.GetRequiredService<IMessageRoutingKeyResolver>();
            return new RabbitMqMessagePublisher(options.ExchangeName, rabbitMqClientAdapter, routingKeyResolver);
        });
    }

    private static void AddAzureServiceBusMessagingServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceBusPublisherOptions>(configuration.GetSection(ServiceBusPublisherOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceBusPublisherOptions>>().Value;

            var missingProperties = new List<string>();
            if (string.IsNullOrWhiteSpace(options.ConnectionString) && string.IsNullOrWhiteSpace(options.Namespace))
            {
                missingProperties.Add($"{nameof(ServiceBusPublisherOptions.ConnectionString)} or {nameof(ServiceBusPublisherOptions.Namespace)}");
            }

            if (string.IsNullOrWhiteSpace(options.TopicName))
            {
                missingProperties.Add(nameof(ServiceBusPublisherOptions.TopicName));
            }

            if (string.IsNullOrWhiteSpace(options.Subject))
            {
                missingProperties.Add(nameof(ServiceBusPublisherOptions.Subject));
            }

            if (string.IsNullOrWhiteSpace(options.RoutingKey))
            {
                missingProperties.Add(nameof(ServiceBusPublisherOptions.RoutingKey));
            }

            if (string.IsNullOrWhiteSpace(options.EventType))
            {
                missingProperties.Add(nameof(ServiceBusPublisherOptions.EventType));
            }

            if (missingProperties.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Azure Service Bus publisher configuration is incomplete. Missing values: {string.Join(", ", missingProperties)}");
            }

            return options;
        });

        services.AddSingleton<IServiceBusMessageSender>(sp =>
        {
            var options = sp.GetRequiredService<ServiceBusPublisherOptions>();
            return new ServiceBusMessageSender(options.ConnectionString, options.Namespace, options.TopicName);
        });

        services.AddSingleton<IMessagePublisher>(sp =>
        {
            var sender = sp.GetRequiredService<IServiceBusMessageSender>();
            var options = sp.GetRequiredService<ServiceBusPublisherOptions>();
            return new ServiceBusMessagePublisher(
                sender,
                options.TopicName,
                options.Subject,
                options.RoutingKey,
                options.EventType);
        });
    }
}
