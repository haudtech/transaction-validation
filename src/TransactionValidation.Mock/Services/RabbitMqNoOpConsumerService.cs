#nullable enable

using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TransactionValidation.Messaging;
using TransactionValidation.Mock.Options;

namespace TransactionValidation.Mock.Services;

/// <summary>
/// Background service that polls RabbitMQ for transaction messages and logs their receipt without altering business data.
/// This supports the local queue-consumer scenario described in the architecture and testing guidance.
/// </summary>
public sealed class RabbitMqNoOpConsumerService : BackgroundService
{
    private readonly RabbitMqConsumerOptions _options;
    private readonly ILogger<RabbitMqNoOpConsumerService> _logger;

    /// <summary>
    /// Initializes the background consumer with the configured RabbitMQ connection settings and logger.
    /// </summary>
    /// <param name="options">Queue consumer configuration values.</param>
    /// <param name="logger">Logger used for consumption diagnostics.</param>
    public RabbitMqNoOpConsumerService(
        IOptions<RabbitMqConsumerOptions> options,
        ILogger<RabbitMqNoOpConsumerService> logger)
    {
        _options = options.Value;
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
        var connection = await RabbitMqApiCompat.CreateConnectionAsync(
            _options.HostName,
            _options.Port,
            _options.UserName,
            _options.Password,
            stoppingToken);
        try
        {
            var channel = await RabbitMqApiCompat.CreateChannelAsync(connection, stoppingToken);
            try
            {
                await DeclareQueueIfNeededAsync(channel, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var delivery = await BasicGetAsync(channel, stoppingToken);

                    if (delivery is null)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.PollIntervalMilliseconds)), stoppingToken);
                        continue;
                    }

                    var deliveryTag = GetDeliveryTag(delivery);

                    _logger.LogInformation("Consumed message from queue {QueueName}. DeliveryTag={DeliveryTag}", _options.QueueName, deliveryTag);

                    if (!_options.AutoAck && deliveryTag.HasValue)
                    {
                        await AckAsync(channel, deliveryTag.Value, stoppingToken);
                    }
                }
            }
            finally
            {
                await RabbitMqApiCompat.DisposeAsync(channel);
            }
        }
        finally
        {
            await RabbitMqApiCompat.DisposeAsync(connection);
        }
    }

    /// <summary>
    /// Declares the configured queue if it is not already present on the broker.
    /// </summary>
    /// <param name="channel">Current RabbitMQ channel.</param>
    /// <param name="cancellationToken">Token used by async broker calls.</param>
    private async Task DeclareQueueIfNeededAsync(object channel, CancellationToken cancellationToken)
    {
        var declared = await RabbitMqApiCompat.TryInvokeAsync(
            channel,
            "QueueDeclareAsync",
            _options.QueueName,
            _options.Durable,
            false,
            false,
            null,
            false,
            false,
            cancellationToken);

        if (!declared)
        {
            declared = await RabbitMqApiCompat.TryInvokeAsync(
                channel,
                "QueueDeclareAsync",
                _options.QueueName,
                _options.Durable,
                false,
                false,
                null,
                false,
            cancellationToken);
        }

        if (!declared)
        {
            declared = await RabbitMqApiCompat.TryInvokeAsync(
                channel,
                "QueueDeclareAsync",
                _options.QueueName,
                _options.Durable,
                false,
                false,
                null);
        }

        if (!declared)
        {
            await RabbitMqApiCompat.InvokeRequiredAsync(channel, "QueueDeclare", _options.QueueName, _options.Durable, false, false, null);
        }
    }

    /// <summary>
    /// Attempts to fetch a single message from the queue using the most compatible API signature available on the client library.
    /// </summary>
    /// <param name="channel">Current RabbitMQ channel.</param>
    /// <param name="cancellationToken">Token used by async broker calls.</param>
    /// <returns>The delivery result returned by the broker, if one exists.</returns>
    private async Task<object?> BasicGetAsync(object channel, CancellationToken cancellationToken)
    {
        var asyncResult = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "BasicGetAsync", _options.QueueName, _options.AutoAck, cancellationToken);
        if (asyncResult.found)
        {
            return asyncResult.result;
        }

        asyncResult = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "BasicGetAsync", _options.QueueName, _options.AutoAck);
        if (asyncResult.found)
        {
            return asyncResult.result;
        }

        var syncResult = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "BasicGet", _options.QueueName, _options.AutoAck);
        if (syncResult.found)
        {
            return syncResult.result;
        }

        throw new InvalidOperationException("Unable to read messages using the available RabbitMQ client API.");
    }

    /// <summary>
    /// Extracts the broker delivery tag from either a BasicGetResult or a reflection-accessible object.
    /// </summary>
    /// <param name="delivery">The broker delivery result.</param>
    /// <returns>The delivery tag when it can be resolved; otherwise null.</returns>
    private static ulong? GetDeliveryTag(object? delivery)
    {
        if (delivery is null)
        {
            return null;
        }

        if (delivery is BasicGetResult result)
        {
            return result.DeliveryTag;
        }

        var property = delivery.GetType().GetProperty("DeliveryTag", BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(delivery);
        return value switch
        {
            ulong tag => tag,
            long longTag => (ulong)longTag,
            int intTag => (ulong)intTag,
            _ => null,
        };
    }

    /// <summary>
    /// Acknowledges the consumed message after it has been processed by the mock consumer.
    /// </summary>
    /// <param name="channel">Current RabbitMQ channel.</param>
    /// <param name="deliveryTag">Broker-generated delivery tag for the consumed message.</param>
    /// <param name="cancellationToken">Token used by async broker calls.</param>
    private async Task AckAsync(object channel, ulong deliveryTag, CancellationToken cancellationToken)
    {
        var acked = await RabbitMqApiCompat.TryInvokeAsync(channel, "BasicAckAsync", deliveryTag, false, cancellationToken);
        if (!acked)
        {
            acked = await RabbitMqApiCompat.TryInvokeAsync(channel, "BasicAckAsync", deliveryTag, false);
        }

        if (!acked)
        {
            await RabbitMqApiCompat.InvokeRequiredAsync(channel, "BasicAck", deliveryTag, false);
        }
    }
}