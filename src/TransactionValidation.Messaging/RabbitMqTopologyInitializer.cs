using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TransactionValidation.Messaging;

/// <summary>
/// Declares the publisher exchange when the application starts.
/// </summary>
public sealed class RabbitMqTopologyInitializer : IHostedService
{
    private readonly string _exchangeName;
    private readonly string _exchangeType;
    private readonly bool _durable;
    private readonly string _alternateExchangeName;
    private readonly string _unroutedQueueName;
    private readonly IRabbitMqClientAdapter _adapter;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    /// <summary>
    /// Initializes the topology initializer with exchange settings and adapter dependencies.
    /// </summary>
    /// <param name="exchangeName">Exchange name.</param>
    /// <param name="exchangeType">Exchange type.</param>
    /// <param name="durable">Whether the exchange should survive broker restarts.</param>
    /// <param name="alternateExchangeName">Exchange receiving unroutable messages.</param>
    /// <param name="unroutedQueueName">Queue receiving messages from the alternate exchange.</param>
    /// <param name="adapter">RabbitMQ adapter used to declare and bind topology.</param>
    /// <param name="logger">Logger for topology initialization diagnostics.</param>
    public RabbitMqTopologyInitializer(
        string exchangeName,
        string exchangeType,
        bool durable,
        string alternateExchangeName,
        string unroutedQueueName,
        IRabbitMqClientAdapter adapter,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _exchangeName = exchangeName;
        _exchangeType = exchangeType;
        _durable = durable;
        _alternateExchangeName = alternateExchangeName;
        _unroutedQueueName = unroutedQueueName;
        _adapter = adapter;
        _logger = logger;
    }

    /// <summary>
    /// Declares the configured exchange without preventing application startup when the broker is unavailable.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel initialization.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        var retryDelay = TimeSpan.FromMilliseconds(500);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _adapter.DeclareExchangeAsync(
                    _alternateExchangeName,
                    "fanout",
                    _durable,
                    new Dictionary<string, object>(),
                    cancellationToken: cancellationToken);

                await _adapter.DeclareDurableQueueAsync(
                    _unroutedQueueName,
                    _durable,
                    cancellationToken);

                await _adapter.BindQueueAsync(
                    _unroutedQueueName,
                    _alternateExchangeName,
                    string.Empty,
                    cancellationToken);

                await _adapter.DeclareExchangeAsync(
                    _exchangeName,
                    _exchangeType,
                    _durable,
                    new Dictionary<string, object>
                    {
                        ["alternate-exchange"] = _alternateExchangeName
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "RabbitMQ exchange declared. Exchange={ExchangeName}, Type={ExchangeType}",
                    _exchangeName,
                    _exchangeType);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "RabbitMQ topology initialization attempt {Attempt} of {MaxAttempts} failed. Retrying.",
                    attempt,
                    maxAttempts);
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "RabbitMQ exchange declaration failed. Exchange={ExchangeName}; application startup will continue.",
                    _exchangeName);
            }
        }
    }

    /// <summary>
    /// Completes hosted-service shutdown without additional broker operations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel shutdown.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
