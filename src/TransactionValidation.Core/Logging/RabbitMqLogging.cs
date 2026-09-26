using Microsoft.Extensions.Logging;

namespace TransactionValidation.Core.Logging;

/// <summary>
/// Source-generated structured logging events for RabbitMQ topology and consumer lifecycle operations.
/// </summary>
public static partial class RabbitMqLogging
{
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Information,
        Message = "RabbitMQ exchange declared. Exchange={ExchangeName} Type={ExchangeType}")]
    public static partial void ExchangeDeclared(
        ILogger logger,
        string exchangeName,
        string exchangeType);

    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Warning,
        Message = "RabbitMQ topology initialization attempt {Attempt} of {MaxAttempts} failed. Retrying.")]
    public static partial void TopologyInitializationRetrying(
        ILogger logger,
        Exception exception,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 1402,
        Level = LogLevel.Error,
        Message = "RabbitMQ exchange declaration failed. Exchange={ExchangeName}; application startup will continue.")]
    public static partial void ExchangeDeclarationFailed(
        ILogger logger,
        Exception exception,
        string exchangeName);

    [LoggerMessage(
        EventId = 1403,
        Level = LogLevel.Information,
        Message = "RabbitMQ audit consumer starting. Queue={QueueName} BindingPattern={BindingPattern}")]
    public static partial void AuditConsumerStarting(
        ILogger logger,
        string queueName,
        string bindingPattern);

    [LoggerMessage(
        EventId = 1404,
        Level = LogLevel.Information,
        Message = "RabbitMQ primary consumer starting. Queue={QueueName} AutoAck={AutoAck} PollIntervalMs={PollInterval}")]
    public static partial void PrimaryConsumerStarting(
        ILogger logger,
        string queueName,
        bool autoAck,
        int pollInterval);
}
