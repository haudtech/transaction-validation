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
    private readonly IRabbitMqClientAdapter _adapter;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    /// <summary>
    /// Initializes the topology initializer with exchange settings and adapter dependencies.
    /// </summary>
    /// <param name="exchangeName">Exchange name.</param>
    /// <param name="exchangeType">Exchange type.</param>
    /// <param name="durable">Whether the exchange should survive broker restarts.</param>
    /// <param name="adapter">RabbitMQ adapter used to declare the exchange.</param>
    /// <param name="logger">Logger for topology initialization diagnostics.</param>
    public RabbitMqTopologyInitializer(
        string exchangeName,
        string exchangeType,
        bool durable,
        IRabbitMqClientAdapter adapter,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _exchangeName = exchangeName;
        _exchangeType = exchangeType;
        _durable = durable;
        _adapter = adapter;
        _logger = logger;
    }

    /// <summary>
    /// Declares the configured exchange without preventing application startup when the broker is unavailable.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel initialization.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _adapter.DeclareExchangeAsync(
                _exchangeName,
                _exchangeType,
                _durable,
                cancellationToken);

            _logger.LogInformation(
                "RabbitMQ exchange declared. Exchange={ExchangeName}, Type={ExchangeType}",
                _exchangeName,
                _exchangeType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ exchange declaration failed. Exchange={ExchangeName}; application startup will continue.",
                _exchangeName);
        }
    }

    /// <summary>
    /// Completes hosted-service shutdown without additional broker operations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel shutdown.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
