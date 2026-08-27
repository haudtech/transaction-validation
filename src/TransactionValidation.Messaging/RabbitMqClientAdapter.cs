using System.Text;

using RabbitMQ.Client;

namespace TransactionValidation.Messaging;

/// <summary>
/// RabbitMQ.Client 7.0.0 adapter that creates connections, declares durable queues and exchanges, and publishes persistent messages with broker confirmation.
/// This is the concrete implementation behind the queue publishing flow described in the architecture design.
/// </summary>
public sealed class RabbitMqClientAdapter : IRabbitMqClientAdapter, IAsyncDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqClientAdapter"/> class.
    /// </summary>
    /// <param name="hostName">RabbitMQ broker host name.</param>
    /// <param name="port">RabbitMQ broker port.</param>
    /// <param name="userName">RabbitMQ username.</param>
    /// <param name="password">RabbitMQ password.</param>
    /// <param name="publishConfirmTimeoutSeconds">Retained for configuration compatibility; RabbitMQ.Client 7 awaits confirmation as part of <c>BasicPublishAsync</c>.</param>
    public RabbitMqClientAdapter(string hostName, int port, string userName, string password, int publishConfirmTimeoutSeconds = 5)
    {
        _connectionFactory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password
        };
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null)
        {
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        }

        _channel ??= await _connection.CreateChannelAsync(
            new CreateChannelOptions(true, true, null, null),
            cancellationToken);
        return _channel;
    }

    private async Task ResetResourcesAsync()
    {
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;

        if (channel is not null)
        {
            await channel.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Releases the shared RabbitMQ channel and connection owned by the adapter.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _operationLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await ResetResourcesAsync();
        }
        finally
        {
            _operationLock.Release();
            _operationLock.Dispose();
        }
    }

    /// <summary>
    /// Declares a queue with durable/non-durable semantics using a compatible API path.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="durable">Whether the queue should be durable.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    public async Task DeclareDurableQueueAsync(string queueName, bool durable, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.QueueDeclareAsync(
                queueName,
                durable,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken);
        }
        catch
        {
            await ResetResourcesAsync();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Declares a durable exchange using the compatible RabbitMQ client API.
    /// </summary>
    /// <param name="exchangeName">Exchange name.</param>
    /// <param name="exchangeType">Exchange type, such as <c>topic</c>.</param>
    /// <param name="durable">Whether the exchange should survive broker restarts.</param>
    /// <param name="cancellationToken">Cancellation token used by async broker calls.</param>
    public async Task DeclareExchangeAsync(
        string exchangeName,
        string exchangeType,
        bool durable,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.ExchangeDeclareAsync(
                exchangeName,
                exchangeType,
                durable,
                autoDelete: false,
                arguments: arguments.ToDictionary(entry => entry.Key, entry => (object?)entry.Value),
                passive: false,
                noWait: false,
                cancellationToken);
        }
        catch
        {
            await ResetResourcesAsync();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Binds a queue to an exchange using RabbitMQ.Client 7.
    /// </summary>
    /// <param name="queueName">Queue to bind.</param>
    /// <param name="exchangeName">Exchange to bind from.</param>
    /// <param name="routingKey">Binding routing key.</param>
    /// <param name="cancellationToken">Cancellation token used by async broker calls.</param>
    public async Task BindQueueAsync(string queueName, string exchangeName, string routingKey, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.QueueBindAsync(
                queueName,
                exchangeName,
                routingKey,
                arguments: null,
                noWait: false,
                cancellationToken);
        }
        catch
        {
            await ResetResourcesAsync();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Publishes a UTF-8 payload as a persistent message and waits for RabbitMQ.Client 7 broker confirmation.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="payload">Serialized message payload.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns><see langword="true"/> when the confirmation-enabled publish completes successfully.</returns>
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
    /// Publishes a persistent UTF-8 payload to an exchange and waits for RabbitMQ.Client 7 broker confirmation.
    /// </summary>
    /// <param name="exchangeName">Exchange name, or empty for the legacy default exchange.</param>
    /// <param name="routingKey">Broker routing key.</param>
    /// <param name="payload">Serialized message payload.</param>
    /// <param name="headers">Message headers.</param>
    /// <param name="cancellationToken">Cancellation token for the RabbitMQ.Client 7 publish operation.</param>
    /// <returns><see langword="true"/> when the broker confirms the publish.</returns>
    public async Task<bool> PublishPersistentWithConfirmAsync(
        string exchangeName,
        string routingKey,
        string payload,
        IReadOnlyDictionary<string, object> headers,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                Persistent = true,
                Headers = headers.Count > 0
                    ? headers.ToDictionary(entry => entry.Key, entry => (object?)entry.Value)
                    : null
            };

            var body = Encoding.UTF8.GetBytes(payload);
            await channel.BasicPublishAsync(
                exchangeName,
                routingKey,
                mandatory: true,
                properties,
                body,
                cancellationToken);

            return true;
        }
        catch
        {
            await ResetResourcesAsync();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }
}
