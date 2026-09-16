using System.Text.Json;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransactionValidation.Core.Logging;
using TransactionValidation.Core.Models;
using TransactionValidation.Mock.Options;

namespace TransactionValidation.Mock.Services;

/// <summary>
/// Background consumer that reads the primary Azure Service Bus subscription and records each observed event.
/// </summary>
public sealed class ServiceBusPrimaryConsumerService : BackgroundService
{
    private const string ConsumerName = "primary";
    private static readonly string DependencyName = "AzureServiceBus";
    private readonly ServiceBusPrimaryConsumerOptions _options;
    private readonly ConsumerObservationStore _observationStore;
    private readonly ILogger<ServiceBusPrimaryConsumerService> _logger;

    public ServiceBusPrimaryConsumerService(
        IOptions<ServiceBusPrimaryConsumerOptions> options,
        ConsumerObservationStore observationStore,
        ILogger<ServiceBusPrimaryConsumerService> logger)
    {
        _options = options.Value;
        _observationStore = observationStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting primary Service Bus consumer. Topic={TopicName}, Subscription={SubscriptionName}, AutoComplete={AutoComplete}",
            _options.TopicName,
            _options.SubscriptionName,
            _options.AutoComplete);

        await using var client = TransactionValidation.Messaging.ServiceBusClientFactory.Create(_options.ConnectionString, _options.Namespace);
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

            TransactionValidationLogger.ConsumerMessageObserved(
                _logger,
                DependencyName,
                ConsumerName,
                _options.SubscriptionName,
                envelope.MessageId,
                envelope.CorrelationId,
                args.Message.DeliveryCount);

            if (!_options.AutoComplete)
            {
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            TransactionValidationLogger.ConsumerProcessingFailed(
                _logger,
                args.Exception,
                DependencyName,
                ConsumerName,
                args.EntityPath);
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
            throw new InvalidOperationException("Unable to deserialize a Service Bus transaction envelope.");
        }

        return envelope;
    }
}
