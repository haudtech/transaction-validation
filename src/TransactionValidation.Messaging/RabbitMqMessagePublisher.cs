using System.Text.Json;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

/// <summary>
/// Serializes and publishes transaction envelopes to RabbitMQ after declaring the queue and waiting for broker confirmation.
/// This class implements the accepted-message publication step of the API-to-queue workflow in the design docs.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly string _queueName;
    private readonly bool _durable;
    private readonly IRabbitMqClientAdapter _rabbitMqClientAdapter;

    /// <summary>
    /// Initializes the publisher with the RabbitMQ target queue and adapter used for broker communication.
    /// </summary>
    /// <param name="queueName">Queue name to publish accepted transaction envelopes to.</param>
    /// <param name="durable">Whether the target queue must be durable.</param>
    /// <param name="rabbitMqClientAdapter">Adapter that performs the actual queue and publish operations.</param>
    public RabbitMqMessagePublisher(string queueName, bool durable, IRabbitMqClientAdapter rabbitMqClientAdapter)
    {
        _queueName = queueName;
        _durable = durable;
        _rabbitMqClientAdapter = rabbitMqClientAdapter;
    }

    /// <summary>
    /// Serializes the transaction envelope and publishes it to RabbitMQ only after the queue is declared and confirmed by the broker.
    /// </summary>
    /// <param name="envelope">The internal message containing the accepted transaction and correlation metadata.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ConflictException">Thrown when broker publish confirmation is not received.</exception>
    public async Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(envelope);

        await _rabbitMqClientAdapter.DeclareDurableQueueAsync(_queueName, _durable, cancellationToken);
        var confirmed = await _rabbitMqClientAdapter.PublishPersistentWithConfirmAsync(_queueName, payload, cancellationToken);
        if (!confirmed)
        {
            throw new ConflictException("RabbitMQ did not confirm message publishing.");
        }
    }
}
