using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.LocalBootstrap.Options;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.PolicyStore.Entities;

namespace TransactionValidation.Gateway.LocalBootstrap.Services;

public sealed class LocalClientSeeder(
    GatewayPolicyDbContext dbContext,
    ISecretProvider secretProvider,
    ISecretWriter secretWriter,
    LocalSecretProvisioner provisioner,
    IOptions<LocalBootstrapOptions> options,
    TimeProvider timeProvider)
{
    private const string ApiId = "transaction-validation";
    private const string ApiVersion = "v1";

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await provisioner.EnsurePepperAsync(settings.ClientPepperVersion, cancellationToken);

        if (!await dbContext.Clients.AnyAsync(client => client.ClientId == settings.ClientId, cancellationToken))
        {
            dbContext.Clients.Add(new GatewayClientEntity { ClientId = settings.ClientId, Enabled = true });
        }

        if (!await dbContext.ClientApiGrants.AnyAsync(
                grant => grant.ClientId == settings.ClientId
                    && grant.ApiId == ApiId
                    && grant.ApiVersion == ApiVersion,
                cancellationToken))
        {
            dbContext.ClientApiGrants.Add(new GatewayClientApiGrantEntity
            {
                ClientId = settings.ClientId,
                ApiId = ApiId,
                ApiVersion = ApiVersion
            });
        }

        if (!await dbContext.ClientApiLimits.AnyAsync(
                limit => limit.ClientId == settings.ClientId && limit.ApiId == ApiId,
                cancellationToken))
        {
            dbContext.ClientApiLimits.Add(new GatewayClientApiLimitEntity
            {
                ClientId = settings.ClientId,
                ApiId = ApiId,
                RatePerMinute = settings.RatePerMinute,
                Burst = settings.Burst,
                DailyQuota = settings.DailyQuota,
                MonthlyQuota = settings.MonthlyQuota
            });
        }

        if (await dbContext.ClientKeys.AnyAsync(key => key.KeyId == settings.ClientKeyId, cancellationToken))
        {
            // The one-time secret cannot be recovered from its verifier.
            if (!await provisioner.ExistsAsync(settings.CredentialSecretName, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Client key '{settings.ClientKeyId}' exists but secret '{settings.CredentialSecretName}' is missing. "
                    + "Reset the local PostgreSQL volume to re-seed the client.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var pepper = await secretProvider.GetSecretAsync(settings.ClientPepperVersion, cancellationToken);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var secretAscii = Encoding.ASCII.GetBytes(secret);
        var credentialUtf8 = Encoding.UTF8.GetBytes($"{settings.ClientKeyId}.{secret}");
        try
        {
            var verifier = HMACSHA256.HashData(pepper, secretAscii);
            dbContext.ClientKeys.Add(new GatewayClientKeyEntity
            {
                KeyId = settings.ClientKeyId,
                ClientId = settings.ClientId,
                Verifier = verifier,
                PepperVersion = settings.ClientPepperVersion,
                Status = GatewayClientKeyStatus.Current,
                NotBefore = timeProvider.GetUtcNow()
            });

            // A kept .secrets directory can outlive a reset database; replace the stale credential.
            await secretWriter.DeleteSecretAsync(settings.CredentialSecretName, cancellationToken);
            await secretWriter.WriteSecretAsync(settings.CredentialSecretName, credentialUtf8, cancellationToken);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await secretWriter.DeleteSecretAsync(settings.CredentialSecretName, CancellationToken.None);
                throw;
            }

            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pepper);
            CryptographicOperations.ZeroMemory(secretAscii);
            CryptographicOperations.ZeroMemory(credentialUtf8);
        }
    }
}
