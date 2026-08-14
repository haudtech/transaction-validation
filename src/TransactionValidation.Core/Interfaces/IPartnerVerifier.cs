namespace TransactionValidation.Core.Interfaces;

public interface IPartnerVerifier
{
    Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null);
}
