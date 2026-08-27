using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using TransactionValidation.Core.Models;
using TransactionValidation.Messaging;
using TransactionValidation.Mock.Options;

namespace TransactionValidation.Mock.Services;

/// <summary>
/// Background service that polls RabbitMQ for transaction messages and logs their receipt without altering business data.
/// This supports the local queue-consumer scenario described in the architecture and testing guidance.
/// </summary>
public sealed class RabbitMqNoOpConsumerService : BackgroundService
{
    private readonly RabbitMqPrimaryConsumerOptions _options;
    private readonly ConsumerObservationStore _observationStore;
    private readonly ILogger<RabbitMqNoOpConsumerService> _logger;

    /// <summary>
    /// Initializes the background consumer with the configured RabbitMQ connection settings and logger.
    /// </summary>
    /// <param name="options">Queue consumer configuration values.</param>
    /// <param name="logger">Logger used for consumption diagnostics.</param>
    public RabbitMqNoOpConsumerService(
        IOptions<RabbitMqPrimaryConsumerOptions> options,
        ConsumerObservationStore observationStore,
        ILogger<RabbitMqNoOpConsumerService> logger)
    {
        _options = options.Value;
        _observationStore = observationStore;
        _logger = logger;
    }

    /// <summary>
    /// Runs the background polling loop and retries on transient failures while the host is alive.
    /// </summary>
    /// <param name="stoppingToken">Token used to terminate the consumer during host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting no-op RabbitMQ consumer. Queue={QueueName}, AutoAck={AutoAck}, PollIntervalMs={PollInterval}",
            _options.QueueName,
            _options.AutoAck,
            _options.PollIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consume loop failed. Retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Opens a RabbitMQ connection and repeatedly polls the queue for messages until cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">Token that can interrupt the consume loop.</param>
    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(false, false, null, null),
            stoppingToken);
        await DeclareQueueIfNeededAsync(channel, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delivery = await channel.BasicGetAsync(_options.QueueName, _options.AutoAck, stoppingToken);

            if (delivery is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.PollIntervalMilliseconds)), stoppingToken);
                continue;
            }

            var deliveryTag = delivery.DeliveryTag;
            var envelope = JsonSerializer.Deserialize<TransactionEnvelope>(delivery.Body.Span);
            if (envelope is null)
            {
                throw new InvalidOperationException("Unable to deserialize a transaction envelope.");
            }

            _observationStore.Add(new ConsumerObservation(
                "primary",
                _options.QueueName,
                envelope.MessageId,
                envelope.CorrelationId,
                delivery.RoutingKey,
                delivery.Redelivered,
                1,
                DateTimeOffset.UtcNow));

            _logger.LogInformation("Consumed message from queue {QueueName}. DeliveryTag={DeliveryTag}", _options.QueueName, deliveryTag);

            if (!_options.AutoAck)
            {
                await channel.BasicAckAsync(deliveryTag, multiple: false, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Declares the configured queue if it is not already present on the broker.
    /// </summary>
    /// <param name="channel">Current RabbitMQ channel.</param>
    /// <param name="cancellationToken">Token used by async broker calls.</param>
    private async Task DeclareQueueIfNeededAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            _options.AlternateExchangeName,
            "fanout",
            _options.Durable,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        // Declare the unrouted queue that receives messages that don't match any binding pattern
        await channel.QueueDeclareAsync(
            _options.UnroutedQueueName,
            _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        // Bind the unrouted queue to the alternate exchange with empty routing key (fanout receives all messages)
        await channel.QueueBindAsync(
            _options.UnroutedQueueName,
            _options.AlternateExchangeName,
            string.Empty,
            arguments: null,
            noWait: false,
            cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            "topic",
            _options.Durable,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["alternate-exchange"] = _options.AlternateExchangeName
            },
            passive: false,
            noWait: false,
            cancellationToken);

        await channel.QueueDeclareAsync(
            _options.QueueName,
            _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        await channel.QueueBindAsync(
            _options.QueueName,
            _options.ExchangeName,
            _options.BindingPattern,
            arguments: null,
            noWait: false,
            cancellationToken);
    }

}
