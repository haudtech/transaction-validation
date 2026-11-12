using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface IClientCredentialStore
{
    Task<ClientCredentialRecord?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken);
}
