namespace TransactionValidation.Mock.Options;

/// <summary>
/// Common RabbitMQ configuration shared by the Mock project's independent consumers.
/// </summary>
public abstract class RabbitMqConsumerOptions
{
    public required bool Enabled { get; set; }

    public required string HostName { get; set; }

    public required int Port { get; set; }

    public required string UserName { get; set; }

    public required string Password { get; set; }

    public required string ExchangeName { get; set; }

    public required bool Durable { get; set; }

    public required bool AutoAck { get; set; }

    public required int PollIntervalMilliseconds { get; set; }

}
