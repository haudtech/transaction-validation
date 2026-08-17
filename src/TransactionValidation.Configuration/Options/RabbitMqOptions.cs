namespace TransactionValidation.Configuration.Options;

/// <summary>
/// RabbitMQ connection and queue options for the BFF publisher.
/// These values are used to connect to the local broker described in the architecture and Docker guidance.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "partner-transactions";

    public bool Durable { get; set; } = true;
}
