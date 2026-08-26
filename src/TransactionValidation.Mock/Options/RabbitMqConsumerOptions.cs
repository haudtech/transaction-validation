namespace TransactionValidation.Mock.Options;

/// <summary>
/// Configuration for the local RabbitMQ consumer used to observe queued transaction envelopes during smoke testing and development.
/// </summary>
public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMqConsumer";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "partner-transactions";

    public string ExchangeName { get; set; } = "partner.transactions";

    public string AlternateExchangeName { get; set; } = "partner.transactions.unrouted";

    public string UnroutedQueueName { get; set; } = "partner-transactions.unrouted";

    public string BindingPattern { get; set; } = "partner.transaction.#";

    public bool Durable { get; set; } = true;

    public bool AutoAck { get; set; } = false;

    public int PollIntervalMilliseconds { get; set; } = 750;
}