using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface IBackendCredentialStore
{
    Task<BackendCredentialSecretReference?> GetCurrentAsync(
        string apiId,
        CancellationToken cancellationToken);

    Task<BackendCredentialRecord?> GetByKeyIdAsync(
        string apiId,
        string keyId,
        CancellationToken cancellationToken);
}
