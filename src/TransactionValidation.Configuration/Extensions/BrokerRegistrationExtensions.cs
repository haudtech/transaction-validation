using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Configuration.Options;

namespace TransactionValidation.Configuration.Extensions;

/// <summary>
/// Exposes a centralized broker selection configuration for the local hosting environment.
/// </summary>
public static class BrokerRegistrationExtensions
{
    /// <summary>
    /// Registers the broker selection and dispatches to the selected broker-specific registration action.
    /// </summary>
    /// <param name="services">The dependency injection container being configured.</param>
    /// <param name="configuration">The configuration source used to resolve the broker selection.</param>
    /// <param name="rabbitMqRegistration">Registration callback for the RabbitMQ path.</param>
    /// <param name="azureServiceBusRegistration">Registration callback for the Azure Service Bus path.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddConfiguredBroker(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceCollection, IConfiguration> rabbitMqRegistration,
        Action<IServiceCollection, IConfiguration>? azureServiceBusRegistration = null)
    {
        services.Configure<BrokerTypeOptions>(configuration.GetSection(BrokerTypeOptions.SectionName));

        var brokerType = configuration
            .GetSection(BrokerTypeOptions.SectionName)
            .Get<BrokerTypeOptions>()
            ?? new BrokerTypeOptions();

        switch (brokerType.BrokerType)
        {
            case BrokerTypeOptions.RabbitMq:
                rabbitMqRegistration(services, configuration);
                return services;

            case BrokerTypeOptions.AzureServiceBus:
                if (azureServiceBusRegistration is null)
                {
                    throw new InvalidOperationException("Azure Service Bus broker registration is not configured.");
                }

                azureServiceBusRegistration(services, configuration);
                return services;

            default:
                throw new InvalidOperationException($"Unsupported broker configuration: {brokerType.BrokerType}");
        }
    }

    /// <summary>
    /// Resolves the active broker selection from configuration, returning the default RabbitMQ option when no explicit broker is configured.
    /// </summary>
    /// <param name="configuration">The configuration source used to resolve the active broker.</param>
    /// <returns>The configured broker selection.</returns>
    public static BrokerTypeOptions GetBrokerSelection(this IConfiguration configuration)
    {
        return configuration
            .GetSection(BrokerTypeOptions.SectionName)
            .Get<BrokerTypeOptions>()
            ?? new BrokerTypeOptions();
    }
}
