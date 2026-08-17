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

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqClientAdapter"/> class.
    /// </summary>
    /// <param name="hostName">RabbitMQ broker host name.</param>
    /// <param name="port">RabbitMQ broker port.</param>
    /// <param name="userName">RabbitMQ username.</param>
    /// <param name="password">RabbitMQ password.</param>
    public RabbitMqClientAdapter(string hostName, int port, string userName, string password)
    {
        _hostName = hostName;
        _port = port;
        _userName = userName;
        _password = password;
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
    /// Publishes a UTF-8 payload as a persistent message and waits for broker publish confirmation when available.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="payload">Serialized message payload.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns><see langword="true"/> when publish confirmation succeeds or is not supported; otherwise <see langword="false"/>.</returns>
    public async Task<bool> PublishPersistentWithConfirmAsync(string queueName, string payload, CancellationToken cancellationToken = default)
    {
        var connection = await RabbitMqApiCompat.CreateConnectionAsync(_hostName, _port, _userName, _password, cancellationToken);
        try
        {
            var channel = await RabbitMqApiCompat.CreateChannelAsync(connection, cancellationToken);
            try
            {
                await RabbitMqApiCompat.TryInvokeAsync(channel, "ConfirmSelectAsync", cancellationToken);
                await RabbitMqApiCompat.TryInvokeAsync(channel, "ConfirmSelect");

                object? properties = null;
                var basicPropertiesCreated = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "CreateBasicProperties");
                if (basicPropertiesCreated.found)
                {
                    properties = basicPropertiesCreated.result;
                }

                // RabbitMQ.Client v7 generic BasicPublishAsync<TProperties> requires a non-null TProperties value.
                // If channel-specific creation API is unavailable, fall back to a concrete basic properties instance.
                properties ??= new BasicProperties();

                if (properties is not null)
                {
                    var persistentProperty = properties?.GetType().GetProperty("Persistent", BindingFlags.Public | BindingFlags.Instance);
                    if (persistentProperty?.CanWrite == true)
                    {
                        persistentProperty.SetValue(properties, true);
                    }
                }

                var body = Encoding.UTF8.GetBytes(payload);
                ReadOnlyMemory<byte> bodyMemory = body;

                var publishedAsync = await RabbitMqApiCompat.TryInvokeAsync(
                    channel,
                    "BasicPublishAsync",
                    string.Empty,
                    queueName,
                    true,
                    properties,
                    bodyMemory,
                    cancellationToken);

                if (!publishedAsync)
                {
                    publishedAsync = await RabbitMqApiCompat.TryInvokeAsync(
                        channel,
                        "BasicPublishAsync",
                        string.Empty,
                        queueName,
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
                        string.Empty,
                        queueName,
                        true,
                        properties,
                        bodyMemory);
                }

                if (!publishedAsync)
                {
                    await RabbitMqApiCompat.InvokeRequiredAsync(channel, "BasicPublish", string.Empty, queueName, properties, body);
                }

                var confirmAsync = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "WaitForConfirmsAsync", cancellationToken);
                if (confirmAsync.found)
                {
                    return confirmAsync.result as bool? ?? true;
                }

                var confirmSync = await RabbitMqApiCompat.TryInvokeWithResultAsync(channel, "WaitForConfirms", TimeSpan.FromSeconds(5));
                if (confirmSync.found)
                {
                    return confirmSync.result as bool? ?? true;
                }

                return true;
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
