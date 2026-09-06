using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace TransactionValidation.Messaging;

/// <summary>
/// Builds a Service Bus client from a connection string when present, otherwise falls back to
/// Managed Identity (<see cref="DefaultAzureCredential"/>) against the configured namespace FQDN.
/// </summary>
public static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(string connectionString, string @namespace)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new ServiceBusClient(connectionString);
        }

        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new InvalidOperationException(
                "Either a Service Bus connection string or a namespace (for Managed Identity auth) must be configured.");
        }

        return new ServiceBusClient(@namespace, new DefaultAzureCredential());
    }
}
