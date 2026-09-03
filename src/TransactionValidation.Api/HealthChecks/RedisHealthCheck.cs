using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

namespace TransactionValidation.Api.HealthChecks;

/// <summary>
/// Pings the distributed idempotency store when Redis is configured; reports healthy (no-op) when running with the in-memory fallback.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _connectionMultiplexer;

    public RedisHealthCheck(IServiceProvider serviceProvider)
    {
        _connectionMultiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_connectionMultiplexer is null)
        {
            return HealthCheckResult.Healthy("Redis not configured; using in-memory idempotency store.");
        }

        try
        {
            await _connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}
