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
    /// Initializes a new instance of the <see cref="RabbitMqMessagePublisher"/> class.
    /// </summary>
    /// <param name="queueName">Target queue name used for publication.</param>
    /// <param name="durable">Whether the target queue should be durable.</param>
    /// <param name="rabbitMqClientAdapter">RabbitMQ adapter used for queue declaration and publish operations.</param>
    public RabbitMqMessagePublisher(string queueName, bool durable, IRabbitMqClientAdapter rabbitMqClientAdapter)
    {
        _queueName = queueName;
        _durable = durable;
        _rabbitMqClientAdapter = rabbitMqClientAdapter;
    }

    /// <summary>
    /// Serializes and publishes the transaction envelope as a persistent RabbitMQ message.
    /// </summary>
    /// <param name="envelope">Transaction envelope to publish.</param>
    /// <param name="cancellationToken">Cancellation token for async queue and publish operations.</param>
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
