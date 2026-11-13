using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface IClientRequestLimiter
{
    /// <exception cref="Exceptions.GatewayRequestLimiterUnavailableException">The limiter store is unavailable.</exception>
    Task<RequestLimitDecision> TryAcquireAsync(
        string clientId,
        ClientApiLimit limit,
        CancellationToken cancellationToken);
}
