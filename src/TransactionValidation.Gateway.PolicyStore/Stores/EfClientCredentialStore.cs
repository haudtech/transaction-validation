using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Stores;

public sealed class EfClientCredentialStore(GatewayPolicyDbContext dbContext) : IClientCredentialStore
{
    public async Task<ClientCredentialRecord?> GetByKeyIdAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await (
                from key in dbContext.ClientKeys.AsNoTracking()
                join grant in dbContext.ClientApiGrants.AsNoTracking()
                    on key.ClientId equals grant.ClientId into clientGrants
                from grant in clientGrants.DefaultIfEmpty()
                where key.KeyId == keyId
                select new
                {
                    key.KeyId,
                    key.ClientId,
                    key.Verifier,
                    key.PepperVersion,
                    key.Status,
                    key.NotBefore,
                    key.ExpiresAt,
                    key.RevokedAt,
                    key.PreviousSince,
                    ClientEnabled = key.Client.Enabled,
                    ApiId = grant == null ? null : grant.ApiId,
                    ApiVersion = grant == null ? null : grant.ApiVersion
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            var credential = rows[0];
            var grants = rows
                .Where(row => row.ApiId is not null)
                .Select(row => new ClientApiGrant(row.ApiId!, row.ApiVersion!))
                .OrderBy(grant => grant.ApiId)
                .ThenBy(grant => grant.ApiVersion)
                .ToArray();

            var limits = await dbContext.ClientApiLimits.AsNoTracking()
                .Where(limit => limit.ClientId == credential.ClientId)
                .OrderBy(limit => limit.ApiId)
                .Select(limit => new ClientApiLimit(
                    limit.ApiId,
                    limit.RatePerMinute,
                    limit.Burst,
                    limit.DailyQuota,
                    limit.MonthlyQuota))
                .ToListAsync(cancellationToken);

            return new ClientCredentialRecord(
                credential.KeyId,
                credential.ClientId,
                credential.Verifier,
                credential.PepperVersion,
                credential.Status,
                credential.NotBefore,
                credential.ExpiresAt,
                credential.RevokedAt,
                credential.PreviousSince,
                credential.ClientEnabled,
                grants,
                limits);
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
