using System.Text.Json;

using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

/// <summary>
/// Serializes and publishes transaction envelopes to RabbitMQ through the configured exchange and waits for broker confirmation.
/// This class implements the accepted-message publication step of the API-to-queue workflow in the design docs.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly string _exchangeName;
    private readonly IRabbitMqClientAdapter _rabbitMqClientAdapter;
    private readonly IMessageRoutingKeyResolver _routingKeyResolver;

    /// <summary>
    /// Initializes the publisher with the RabbitMQ exchange, adapter, and routing-key resolver.
    /// </summary>
    /// <param name="exchangeName">Exchange name to publish transaction envelopes to.</param>
    /// <param name="rabbitMqClientAdapter">Adapter that performs the actual queue and publish operations.</param>
    /// <param name="routingKeyResolver">Resolver that selects the broker routing key.</param>
    public RabbitMqMessagePublisher(
        string exchangeName,
        IRabbitMqClientAdapter rabbitMqClientAdapter,
        IMessageRoutingKeyResolver routingKeyResolver)
    {
        _exchangeName = exchangeName;
        _rabbitMqClientAdapter = rabbitMqClientAdapter;
        _routingKeyResolver = routingKeyResolver;
    }

    /// <summary>
    /// Serializes the transaction envelope and publishes it to the exchange after the broker confirms the message.
    /// </summary>
    /// <param name="envelope">The internal message containing the accepted transaction and correlation metadata.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ConflictException">Thrown when broker publish confirmation is not received.</exception>
    public async Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(envelope);
        var routingKey = _routingKeyResolver.Resolve(envelope);
        var headers = new Dictionary<string, object>
        {
            ["message-type"] = "PartnerTransactionAccepted",
            ["message-version"] = "1",
            ["correlation-id"] = envelope.CorrelationId,
            ["message-id"] = envelope.MessageId
        };

        var confirmed = await _rabbitMqClientAdapter.PublishPersistentWithConfirmAsync(
            _exchangeName,
            routingKey,
            payload,
            headers,
            cancellationToken);
        if (!confirmed)
        {
            throw new ConflictException("RabbitMQ did not confirm message publishing.");
        }
    }
}
