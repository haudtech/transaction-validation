namespace TransactionValidation.Messaging;

/// <summary>
/// Low-level abstraction for declaring queues and publishing persistent messages with broker confirmation semantics.
/// This supports the RabbitMQ publish-confirm flow in the architecture design.
/// </summary>
public interface IRabbitMqClientAdapter
{
    Task DeclareDurableQueueAsync(string queueName, bool durable, CancellationToken cancellationToken = default);

    Task<bool> PublishPersistentWithConfirmAsync(string queueName, string payload, CancellationToken cancellationToken = default);
}
