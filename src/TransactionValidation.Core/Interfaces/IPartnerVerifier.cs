namespace TransactionValidation.Core.Interfaces;

/// <summary>
/// Contract for verifying a partner before a transaction is accepted and placed onto the message broker.
/// The implementation is expected to respect the resilience policy described in the solution analysis.
/// </summary>
public interface IPartnerVerifier
{
    Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null);
}
