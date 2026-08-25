#nullable enable

using System.Text;
using System.Reflection;
using RabbitMQ.Client;

namespace TransactionValidation.Messaging;

/// <summary>
/// RabbitMQ client adapter that creates connections, declares durable queues, and publishes messages with compatibility wrappers for different client API versions.
/// This is the concrete implementation behind the queue publishing flow described in the architecture design.
/// </summary>
public sealed class RabbitMqClientAdapter : IRabbitMqClientAdapter
{
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _userName;
    private readonly string _password;
    private readonly TimeSpan _publishConfirmTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqClientAdapter"/> class.
    /// </summary>
    /// <param name="hostName">RabbitMQ broker host name.</param>
    /// <param name="port">RabbitMQ broker port.</param>
    /// <param name="userName">RabbitMQ username.</param>
    /// <param name="password">RabbitMQ password.</param>
    /// <param name="publishConfirmTimeoutSeconds">Maximum wait time for a synchronous broker confirmation.</param>
    public RabbitMqClientAdapter(string hostName, int port, string userName, string password, int publishConfirmTimeoutSeconds = 5)
    {
        _hostName = hostName;
        _port = port;
        _userName = userName;
        _password = password;
        _publishConfirmTimeout = TimeSpan.FromSeconds(Math.Max(1, publishConfirmTimeoutSeconds));
    }

    /// <summary>
    /// Declares a queue with durable/non-durable semantics using a compatible API path.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="durable">Whether the queue should be durable.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    public async Task DeclareDurableQueueAsync(string queueName, bool durable, CancellationToken cancellationToken = default)
    {
        var connection = await RabbitMqApiCompat.CreateConnectionAsync(_hostName, _port, _userName, _password, cancellationToken);
        try
        {
            var channel = await RabbitMqApiCompat.CreateChannelAsync(connection, cancellationToken);
            try
            {
                var declared = await RabbitMqApiCompat.TryInvokeAsync(
                    channel,
                    "QueueDeclareAsync",
                    queueName,
                    durable,
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
                        queueName,
                        durable,
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
                        queueName,
                        durable,
                        false,
                        false,
                        null);
                }

                if (!declared)
                {
                    declared = await RabbitMqApiCompat.TryInvokeAsync(
                        channel,
                        "QueueDeclareAsync",
                        queueName,
                        durable,
                        false,
                        false,
                        null,
                        false,
                        cancellationToken);
                }

                if (!declared)
                {
                    await RabbitMqApiCompat.InvokeRequiredAsync(channel, "QueueDeclare", queueName, durable, false, false, null);
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
    /// Declares a durable exchange using the compatible RabbitMQ client API.
    /// </summary>
    /// <param name="exchangeName">Exchange name.</param>
    /// <param name="exchangeType">Exchange type, such as <c>topic</c>.</param>
    /// <param name="durable">Whether the exchange should survive broker restarts.</param>
    /// <param name="cancellationToken">Cancellation token used by async broker calls.</param>
    public async Task DeclareExchangeAsync(string exchangeName, string exchangeType, bool durable, CancellationToken cancellationToken = default)
    {
        var connection = await RabbitMqApiCompat.CreateConnectionAsync(_hostName, _port, _userName, _password, cancellationToken);
        try
        {
            var channel = await RabbitMqApiCompat.CreateChannelAsync(connection, cancellationToken);
            try
            {
                var declared = await RabbitMqApiCompat.TryInvokeAsync(
                    channel,
                    "ExchangeDeclareAsync",
                    exchangeName,
                    exchangeType,
                    durable,
                    false,
                    null,
                    false,
                    false,
                    cancellationToken);

                if (!declared)
                {
                    declared = await RabbitMqApiCompat.TryInvokeAsync(
                        channel,
                        "ExchangeDeclareAsync",
                        exchangeName,
                        exchangeType,
                        durable,
                        false,
                        null,
                        false,
                        false);
                }

                if (!declared)
                {
                    await RabbitMqApiCompat.InvokeRequiredAsync(
                        channel,
                        "ExchangeDeclare",
                        exchangeName,
                        exchangeType,
                        durable,
                        false,
                        null);
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
    /// Publishes a UTF-8 payload as a persistent message and waits for broker publish confirmation when available.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="payload">Serialized message payload.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns><see langword="true"/> when publish confirmation succeeds or is not supported; otherwise <see langword="false"/>.</returns>
    public async Task<bool> PublishPersistentWithConfirmAsync(string queueName, string payload, CancellationToken cancellationToken = default)
    {
        return await PublishPersistentWithConfirmAsync(
            string.Empty,
            queueName,
            payload,
            new Dictionary<string, object>(),
            cancellationToken);
    }

    /// <summary>
    /// Publishes a persistent UTF-8 payload to an exchange and waits for broker confirmation.
    /// </summary>
    /// <param name="exchangeName">Exchange name, or empty for the legacy default exchange.</param>
    /// <param name="routingKey">Broker routing key.</param>
    /// <param name="payload">Serialized message payload.</param>
    /// <param name="headers">Message headers.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns><see langword="true"/> when the broker confirms the publish; otherwise <see langword="false"/>.</returns>
    public async Task<bool> PublishPersistentWithConfirmAsync(
        string exchangeName,
        string routingKey,
        string payload,
        IReadOnlyDictionary<string, object> headers,
        CancellationToken cancellationToken = default)
    {
        var connection = await RabbitMqApiCompat.CreateConnectionAsync(_hostName, _port, _userName, _password, cancellationToken);
        try
        {
            var channel = await RabbitMqApiCompat.CreateChannelAsync(connection, cancellationToken);
            try
            {
                var confirmEnabled = await RabbitMqApiCompat.TryInvokeAsync(channel, "ConfirmSelectAsync", cancellationToken)
                    || await RabbitMqApiCompat.TryInvokeAsync(channel, "ConfirmSelect");
                if (!confirmEnabled)
                {
                    throw new InvalidOperationException("RabbitMQ publisher confirms are not available in the current client API.");
                }

                object? properties = null;
                var basicPropertiesCreated = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "CreateBasicProperties");
                if (basicPropertiesCreated.found)
                {
                    properties = basicPropertiesCreated.result;
                }

                // RabbitMQ.Client v7 generic BasicPublishAsync<TProperties> requires a non-null TProperties value.
                // If channel-specific creation API is unavailable, fall back to a concrete basic properties instance.
                properties ??= new BasicProperties();

                var persistentProperty = properties.GetType().GetProperty("Persistent", BindingFlags.Public | BindingFlags.Instance);
                if (persistentProperty?.CanWrite == true)
                {
                    persistentProperty.SetValue(properties, true);
                }

                var headersProperty = properties.GetType().GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance);
                if (headersProperty?.CanWrite == true && headers.Count > 0)
                {
                    headersProperty.SetValue(properties, new Dictionary<string, object>(headers));
                }

                var body = Encoding.UTF8.GetBytes(payload);
                ReadOnlyMemory<byte> bodyMemory = body;

                var publishedAsync = await RabbitMqApiCompat.TryInvokeAsync(
                    channel,
                    "BasicPublishAsync",
                    exchangeName,
                    routingKey,
                    true,
                    properties,
                    bodyMemory,
                    cancellationToken);

                if (!publishedAsync)
                {
                    publishedAsync = await RabbitMqApiCompat.TryInvokeAsync(
                        channel,
                        "BasicPublishAsync",
                        exchangeName,
                        routingKey,
                        true,
                        properties,
                        bodyMemory,
                        cancellationToken);
                }

                if (!publishedAsync)
                {
                    publishedAsync = await RabbitMqApiCompat.TryInvokeAsync(
                        channel,
                        "BasicPublishAsync",
                        exchangeName,
                        routingKey,
                        true,
                        properties,
                        bodyMemory);
                }

                if (!publishedAsync)
                {
                    await RabbitMqApiCompat.InvokeRequiredAsync(channel, "BasicPublish", exchangeName, routingKey, properties, body);
                }

                var confirmAsync = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "WaitForConfirmsAsync", cancellationToken);
                if (confirmAsync.found)
                {
                    return confirmAsync.result is bool confirmed && confirmed;
                }

                var confirmSync = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "WaitForConfirms", _publishConfirmTimeout);
                if (confirmSync.found)
                {
                    return confirmSync.result is bool confirmed && confirmed;
                }

                throw new InvalidOperationException("RabbitMQ publisher confirmation is not available in the current client API.");
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
}
