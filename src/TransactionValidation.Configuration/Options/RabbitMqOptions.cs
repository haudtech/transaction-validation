namespace TransactionValidation.Configuration.Options;

/// <summary>
/// RabbitMQ connection, exchange, and compatibility queue options for the BFF publisher.
/// These values are used to connect to the local broker described in the architecture and Docker guidance.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string QueueName { get; init; } = "partner-transactions";

    public string ExchangeName { get; init; } = "partner.transactions";

    public string ExchangeType { get; init; } = "topic";

    public string RoutingKeyPrefix { get; init; } = "partner.transaction";

    public string AlternateExchangeName { get; init; } = "partner.transactions.unrouted";

    public string UnroutedQueueName { get; init; } = "partner-transactions.unrouted";

    public int PublishConfirmTimeoutSeconds { get; init; } = 5;

    public bool Durable { get; init; } = true;
}
