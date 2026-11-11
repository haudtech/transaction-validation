using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts;

namespace TransactionValidation.Gateway.PolicyStore;

public sealed class EfClientCredentialStore(GatewayPolicyDbContext dbContext) : IClientCredentialStore
{
    public async Task<ClientCredentialRecord?> GetByKeyIdAsync(
        string keyId,
        CancellationToken cancellationToken)
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

        return new ClientCredentialRecord(
            credential.KeyId,
            credential.ClientId,
            credential.Verifier,
            credential.PepperVersion,
            credential.Status,
            credential.NotBefore,
            credential.ExpiresAt,
            credential.RevokedAt,
            credential.ClientEnabled,
            grants);
    }
}
