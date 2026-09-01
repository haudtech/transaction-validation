using System.Text.Json;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransactionValidation.Core.Models;
using TransactionValidation.Mock.Options;

namespace TransactionValidation.Mock.Services;

/// <summary>
/// Background consumer that listens to the audit Service Bus subscription and records a second independent observation for the same event.
/// </summary>
public sealed class ServiceBusAuditConsumerService : BackgroundService
{
    private const string ConsumerName = "audit";
    private readonly ServiceBusAuditConsumerOptions _options;
    private readonly ConsumerObservationStore _observationStore;
    private readonly ConsumerFailureControl _failureControl;
    private readonly ILogger<ServiceBusAuditConsumerService> _logger;

    public ServiceBusAuditConsumerService(
        IOptions<ServiceBusAuditConsumerOptions> options,
        ConsumerObservationStore observationStore,
        ConsumerFailureControl failureControl,
        ILogger<ServiceBusAuditConsumerService> logger)
    {
        _options = options.Value;
        _observationStore = observationStore;
        _failureControl = failureControl;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting audit Service Bus consumer. Topic={TopicName}, Subscription={SubscriptionName}, AutoComplete={AutoComplete}",
            _options.TopicName,
            _options.SubscriptionName,
            _options.AutoComplete);

        await using var client = new ServiceBusClient(_options.ConnectionString);
        var processor = client.CreateProcessor(
            _options.TopicName,
            _options.SubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = _options.AutoComplete,
                MaxConcurrentCalls = _options.MaxConcurrentCalls,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        processor.ProcessMessageAsync += async args =>
        {
            var envelope = DeserializeEnvelope(args.Message.Body);
            var routingKey = args.Message.ApplicationProperties.TryGetValue("routingKey", out var value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;

            _observationStore.Add(new ConsumerObservation(
                ConsumerName,
                _options.SubscriptionName,
                envelope.MessageId,
                envelope.CorrelationId,
                routingKey,
                args.Message.DeliveryCount > 1,
                args.Message.DeliveryCount,
                DateTimeOffset.UtcNow));

            _logger.LogInformation(
                "Observed message on audit Service Bus consumer. Subscription={SubscriptionName}, MessageId={MessageId}, CorrelationId={CorrelationId}, RoutingKey={RoutingKey}",
                _options.SubscriptionName,
                envelope.MessageId,
                envelope.CorrelationId,
                routingKey);

            if (!_options.AutoComplete && _failureControl.ShouldFailBeforeAcknowledgement(ConsumerName, envelope.MessageId))
            {
                _logger.LogWarning(
                    "Audit consumer intentionally failed before acknowledgement. MessageId={MessageId}",
                    envelope.MessageId);
                throw new InvalidOperationException("Configured audit consumer failure before acknowledgement.");
            }

            if (!_options.AutoComplete)
            {
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Audit Service Bus processor error. EntityPath={EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        finally
        {
            await processor.StopProcessingAsync(stoppingToken);
        }
    }

    private static TransactionEnvelope DeserializeEnvelope(BinaryData body)
    {
        var json = body.ToString();
        var envelope = JsonSerializer.Deserialize<TransactionEnvelope>(json);
        if (envelope is null)
        {
            throw new InvalidOperationException("Unable to deserialize an audit transaction envelope.");
        }

        return envelope;
    }
}
