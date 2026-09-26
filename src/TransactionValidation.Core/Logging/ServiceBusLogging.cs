using Microsoft.Extensions.Logging;

namespace TransactionValidation.Core.Logging;

/// <summary>
/// Source-generated structured logging events for Azure Service Bus consumer lifecycle operations.
/// </summary>
public static partial class ServiceBusLogging
{
    [LoggerMessage(
        EventId = 1405,
        Level = LogLevel.Information,
        Message = "Azure Service Bus audit consumer starting. Topic={TopicName} Subscription={SubscriptionName} AutoComplete={AutoComplete}")]
    public static partial void AuditConsumerStarting(
        ILogger logger,
        string topicName,
        string subscriptionName,
        bool autoComplete);

    [LoggerMessage(
        EventId = 1406,
        Level = LogLevel.Information,
        Message = "Azure Service Bus primary consumer starting. Topic={TopicName} Subscription={SubscriptionName} AutoComplete={AutoComplete}")]
    public static partial void PrimaryConsumerStarting(
        ILogger logger,
        string topicName,
        string subscriptionName,
        bool autoComplete);
}
