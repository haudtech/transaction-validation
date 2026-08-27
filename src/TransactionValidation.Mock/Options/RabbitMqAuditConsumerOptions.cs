namespace TransactionValidation.Mock.Options;

/// <summary>
/// Configuration for the audit RabbitMQ consumer.
/// </summary>
public sealed class RabbitMqAuditConsumerOptions : RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMqAuditConsumer";

    public required string QueueName { get; set; }

    public required string BindingPattern { get; set; }
}