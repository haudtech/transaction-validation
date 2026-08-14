namespace TransactionValidation.Messaging;

public interface IRabbitMqClientAdapter
{
    Task DeclareDurableQueueAsync(string queueName, bool durable, CancellationToken cancellationToken = default);

    Task<bool> PublishPersistentWithConfirmAsync(string queueName, string payload, CancellationToken cancellationToken = default);
}
