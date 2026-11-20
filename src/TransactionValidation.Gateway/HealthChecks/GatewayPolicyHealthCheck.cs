using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.HealthChecks;

public sealed class GatewayPolicyHealthCheck(GatewayPolicyDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Clients.AsNoTracking().AnyAsync(cancellationToken);
        await dbContext.ClientKeys.AsNoTracking().AnyAsync(cancellationToken);
        await dbContext.ClientApiGrants.AsNoTracking().AnyAsync(cancellationToken);
        await dbContext.ClientApiLimits.AsNoTracking().AnyAsync(cancellationToken);
        await dbContext.BackendCredentials.AsNoTracking().AnyAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
