namespace TransactionValidation.Mock.Options;

public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMqConsumer";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "partner-transactions";

    public bool Durable { get; set; } = true;

    public bool AutoAck { get; set; } = false;

    public int PollIntervalMilliseconds { get; set; } = 750;
}