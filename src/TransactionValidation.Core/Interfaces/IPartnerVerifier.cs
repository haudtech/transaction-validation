namespace TransactionValidation.Core.Interfaces;

/// <summary>
/// Contract for verifying a partner before a transaction is accepted and placed onto the message broker.
/// The registered implementation is executed through the shared HTTP resilience pipeline.
/// </summary>
public interface IPartnerVerifier
{
    Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null);
}
