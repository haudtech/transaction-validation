using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using TransactionValidation.Core.Models;
using TransactionValidation.Mock.Options;

namespace TransactionValidation.Mock.Services;

public sealed class RabbitMqAuditConsumerService : BackgroundService
{
    private const string ConsumerName = "audit";
    private readonly RabbitMqAuditConsumerOptions _options;
    private readonly ConsumerObservationStore _observationStore;
    private readonly ILogger<RabbitMqAuditConsumerService> _logger;
    private readonly ConsumerFailureControl _failureControl;

    public RabbitMqAuditConsumerService(
        IOptions<RabbitMqAuditConsumerOptions> options,
        ConsumerObservationStore observationStore,
        ConsumerFailureControl failureControl,
        ILogger<RabbitMqAuditConsumerService> logger)
    {
        _options = options.Value;
        _observationStore = observationStore;
        _failureControl = failureControl;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting audit RabbitMQ consumer. Queue={QueueName}, BindingPattern={BindingPattern}",
            _options.QueueName,
            _options.BindingPattern);

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
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Audit RabbitMQ consume loop failed. Retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

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

        await channel.QueueDeclareAsync(
            _options.QueueName,
            _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            stoppingToken);
        await channel.QueueBindAsync(
            _options.QueueName,
            _options.ExchangeName,
            _options.BindingPattern,
            arguments: null,
            noWait: false,
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delivery = await channel.BasicGetAsync(_options.QueueName, _options.AutoAck, stoppingToken);
            if (delivery is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.PollIntervalMilliseconds)), stoppingToken);
                continue;
            }

            var envelope = JsonSerializer.Deserialize<TransactionEnvelope>(delivery.Body.Span);
            if (envelope is null)
            {
                throw new InvalidOperationException("Unable to deserialize an audit consumer transaction envelope.");
            }

            _observationStore.Add(new ConsumerObservation(
                ConsumerName,
                _options.QueueName,
                envelope.MessageId,
                envelope.CorrelationId,
                delivery.RoutingKey,
                delivery.Redelivered,
                delivery.Redelivered ? 2 : 1,
                DateTimeOffset.UtcNow));

            _logger.LogInformation(
                "Audit consumer observed message. Queue={QueueName}, MessageId={MessageId}, CorrelationId={CorrelationId}, RoutingKey={RoutingKey}",
                _options.QueueName,
                envelope.MessageId,
                envelope.CorrelationId,
                delivery.RoutingKey);

            if (!_options.AutoAck && _failureControl.ShouldFailBeforeAcknowledgement(ConsumerName, envelope.MessageId))
            {
                _logger.LogWarning(
                    "Audit consumer intentionally failed before acknowledgement. MessageId={MessageId}",
                    envelope.MessageId);
                throw new InvalidOperationException("Configured audit consumer failure before acknowledgement.");
            }

            if (!_options.AutoAck)
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
        }
    }
}
