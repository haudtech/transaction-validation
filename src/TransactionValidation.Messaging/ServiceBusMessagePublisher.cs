using System.Text.Json;

using Azure.Messaging.ServiceBus;

using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

/// <summary>
/// Publishes transaction envelopes to Azure Service Bus using the same abstraction expected by the API contract.
/// </summary>
public sealed class ServiceBusMessagePublisher : IMessagePublisher
{
    private readonly IServiceBusMessageSender _sender;
    private readonly string _topicName;
    private readonly string _subject;
    private readonly string _routingKey;
    private readonly string _eventType;

    public ServiceBusMessagePublisher(
        IServiceBusMessageSender sender,
        string topicName,
        string subject,
        string routingKey,
        string eventType)
    {
        _sender = sender;
        _topicName = topicName;
        _subject = subject;
        _routingKey = routingKey;
        _eventType = eventType;
    }

    public async Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope))
        {
            Subject = _subject,
            CorrelationId = envelope.CorrelationId,
            MessageId = envelope.MessageId,
            To = _topicName
        };

        message.ApplicationProperties["routingKey"] = _routingKey;
        message.ApplicationProperties["eventType"] = _eventType;
        message.ApplicationProperties["message-type"] = "PartnerTransactionAccepted";
        message.ApplicationProperties["message-version"] = "1";
        message.ApplicationProperties["correlation-id"] = envelope.CorrelationId;
        message.ApplicationProperties["message-id"] = envelope.MessageId;

        await _sender.SendMessageAsync(message, cancellationToken);
    }
}
