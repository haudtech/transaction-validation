namespace TransactionValidation.Mock.Options;

/// <summary>
/// Configuration for the primary local RabbitMQ consumer.
/// </summary>
public sealed class RabbitMqPrimaryConsumerOptions : RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMqConsumer";

    public required string QueueName { get; set; }

    public required string AlternateExchangeName { get; set; }

    public required string UnroutedQueueName { get; set; }

    public required string BindingPattern { get; set; }
}