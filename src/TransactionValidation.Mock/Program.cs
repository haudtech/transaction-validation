using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Mock.Options;
using TransactionValidation.Mock.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddTransactionValidationConfiguration(builder.Environment, args);

builder.Services.AddConfiguredBroker(
    builder.Configuration,
    (services, configuration) =>
    {
        services.Configure<RabbitMqPrimaryConsumerOptions>(
            configuration.GetSection(RabbitMqPrimaryConsumerOptions.SectionName));
        services.Configure<RabbitMqAuditConsumerOptions>(
            configuration.GetSection(RabbitMqAuditConsumerOptions.SectionName));
        services.AddSingleton<ConsumerObservationStore>();
        services.AddSingleton<ConsumerFailureControl>();

        if (configuration.GetValue<bool>($"{RabbitMqPrimaryConsumerOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<RabbitMqNoOpConsumerService>();
        }

        if (configuration.GetValue<bool>($"{RabbitMqAuditConsumerOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<RabbitMqAuditConsumerService>();
        }
    },
    (services, configuration) =>
    {
        services.AddSingleton<ConsumerObservationStore>();
        services.AddSingleton<ConsumerFailureControl>();

        if (configuration.GetValue<bool>($"{ServiceBusPrimaryConsumerOptions.SectionName}:Enabled"))
        {
            services.Configure<ServiceBusPrimaryConsumerOptions>(
                configuration.GetSection(ServiceBusPrimaryConsumerOptions.SectionName));
            services.AddHostedService<ServiceBusPrimaryConsumerService>();
        }

        if (configuration.GetValue<bool>($"{ServiceBusAuditConsumerOptions.SectionName}:Enabled"))
        {
            services.Configure<ServiceBusAuditConsumerOptions>(
                configuration.GetSection(ServiceBusAuditConsumerOptions.SectionName));
            services.AddHostedService<ServiceBusAuditConsumerService>();
        }
    });

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation Mock"));
app.MapHealthChecks("/healthz");
app.Run();
