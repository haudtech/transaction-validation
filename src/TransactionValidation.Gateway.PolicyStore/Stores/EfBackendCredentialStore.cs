using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.PolicyStore.Entities;

namespace TransactionValidation.Gateway.PolicyStore.Stores;

public sealed class EfBackendCredentialStore(GatewayPolicyDbContext dbContext) : IBackendCredentialStore
{
    public async Task<BackendCredentialSecretReference?> GetCurrentAsync(
        string apiId,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            return await dbContext.BackendCredentials
                .AsNoTracking()
                .Where(credential =>
                    credential.ApiId == apiId
                    && credential.Status == nameof(GatewayClientKeyStatus.Current)
                    && credential.NotBefore <= now
                    && (credential.ExpiresAt == null || credential.ExpiresAt > now))
                .Select(credential => new BackendCredentialSecretReference(
                    credential.KeyId,
                    credential.SecretName))
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (System.Data.Common.DbException exception)
        {
            throw new GatewayPolicyStoreUnavailableException(
                "The gateway credential store is unavailable.",
                exception);
        }
        catch (TimeoutException exception)
        {
            throw new GatewayPolicyStoreUnavailableException(
                "The gateway credential store timed out.",
                exception);
        }
    }

    public async Task<BackendCredentialRecord?> GetByKeyIdAsync(
        string apiId,
        string keyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = await dbContext.BackendCredentials
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    credential => credential.ApiId == apiId && credential.KeyId == keyId,
                    cancellationToken);

            if (entity is null || !Enum.TryParse<GatewayClientKeyStatus>(entity.Status, out var status))
            {
                return null;
            }

            return new BackendCredentialRecord(
                entity.KeyId,
                entity.ApiId,
                entity.Verifier,
                entity.PepperVersion,
                status,
                entity.NotBefore,
                entity.ExpiresAt,
                entity.RevokedAt,
                entity.PreviousSince);
        }
        catch (System.Data.Common.DbException exception)
        {
            throw new GatewayPolicyStoreUnavailableException(
                "The gateway credential store is unavailable.",
                exception);
        }
        catch (TimeoutException exception)
        {
            throw new GatewayPolicyStoreUnavailableException(
                "The gateway credential store timed out.",
                exception);
        }
    }
}
