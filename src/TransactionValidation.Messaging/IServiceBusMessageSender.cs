using Azure.Messaging.ServiceBus;

namespace TransactionValidation.Messaging;

/// <summary>
/// Sends a prepared Service Bus message to the configured topic without coupling the publisher to the Azure SDK client lifetime.
/// </summary>
public interface IServiceBusMessageSender
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);
}
