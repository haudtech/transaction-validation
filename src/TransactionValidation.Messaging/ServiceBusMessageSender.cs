using Azure.Messaging.ServiceBus;

namespace TransactionValidation.Messaging;

/// <summary>
/// Wraps the Azure Service Bus sender so the publisher depends on a stable interface instead of the SDK client directly.
/// </summary>
public sealed class ServiceBusMessageSender : IServiceBusMessageSender, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusMessageSender(string connectionString, string @namespace, string topicName)
    {
        _client = ServiceBusClientFactory.Create(connectionString, @namespace);
        _sender = _client.CreateSender(topicName);
    }

    public async Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
