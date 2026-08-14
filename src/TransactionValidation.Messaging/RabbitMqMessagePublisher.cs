using System.Text.Json;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly string _queueName;
    private readonly bool _durable;
    private readonly IRabbitMqClientAdapter _rabbitMqClientAdapter;

    public RabbitMqMessagePublisher(string queueName, bool durable, IRabbitMqClientAdapter rabbitMqClientAdapter)
    {
        _queueName = queueName;
        _durable = durable;
        _rabbitMqClientAdapter = rabbitMqClientAdapter;
    }

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
