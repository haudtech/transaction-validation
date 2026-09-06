using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Api.HealthChecks;

/// <summary>
/// Confirms the active broker's message publisher resolves cleanly, catching broker misconfiguration at readiness time.
/// </summary>
public sealed class MessagingHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public MessagingHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _serviceProvider.GetRequiredService<IMessagePublisher>();
            return Task.FromResult(HealthCheckResult.Healthy("Messaging publisher is registered."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Messaging publisher could not be resolved.", ex));
        }
    }
}
