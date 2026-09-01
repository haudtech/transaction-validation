namespace TransactionValidation.Mock.Options;

/// <summary>
/// Common Azure Service Bus configuration shared by the mock project's independent consumer services.
/// </summary>
public abstract class ServiceBusConsumerOptions
{
    public required bool Enabled { get; set; }

    public required string ConnectionString { get; set; }

    public required string TopicName { get; set; }

    public required string SubscriptionName { get; set; }

    public required string Filter { get; set; }

    public required bool AutoComplete { get; set; }

    public required int MaxConcurrentCalls { get; set; }
}
