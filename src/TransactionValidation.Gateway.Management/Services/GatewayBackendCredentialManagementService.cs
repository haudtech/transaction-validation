using System.Security.Cryptography;
using System.Security.Claims;
using System.Data.Common;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Management.Exceptions;
using TransactionValidation.Gateway.Management.Models;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.PolicyStore.Entities;

namespace TransactionValidation.Gateway.Management.Services;

public sealed class GatewayBackendCredentialManagementService(
    GatewayPolicyDbContext dbContext,
    ISecretProvider secretProvider,
    ISecretWriter secretWriter,
    IOptions<BackendCredentialManagementOptions> options,
    TimeProvider timeProvider)
{
    private const string ApiId = "transaction-validation";

    public async Task<IReadOnlyList<BackendCredentialMetadata>> GetCredentialsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.BackendCredentials
                .AsNoTracking()
                .Where(credential => credential.ApiId == ApiId)
                .OrderByDescending(credential => credential.NotBefore)
                .Select(credential => new BackendCredentialMetadata(
                    credential.KeyId,
                    Enum.Parse<GatewayClientKeyStatus>(credential.Status),
                    credential.NotBefore,
                    credential.ExpiresAt,
                    credential.RevokedAt))
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            throw new BackendCredentialManagementUnavailableException(
                "The gateway credential store is unavailable.",
                exception);
        }
    }

    public async Task<IssuedBackendCredential> IssueAsync(
        ClaimsPrincipal administrator,
        CancellationToken cancellationToken)
    {
        var (issuer, subject, clientId) = GetAdministratorIdentity(administrator);
        var pepperVersion = options.Value.PepperVersion;
        byte[] pepper;
        try
        {
            pepper = await secretProvider.GetSecretAsync(pepperVersion, cancellationToken);
        }
        catch (Exception exception) when (IsSecretStorageFailure(exception))
        {
            throw new BackendCredentialManagementUnavailableException(
                "The backend credential secret provider is unavailable.",
                exception);
        }

        if (pepper.Length < 32)
        {
            CryptographicOperations.ZeroMemory(pepper);
            throw new BackendCredentialManagementUnavailableException(
                "The configured backend credential pepper is invalid.",
                new InvalidDataException("Backend credential peppers must contain at least 32 bytes."));
        }

        var keyId = Guid.NewGuid().ToString("N");
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var secretAscii = Encoding.ASCII.GetBytes(secret);
        byte[] verifier;
        try
        {
            verifier = HMACSHA256.HashData(pepper, secretAscii);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretAscii);
        }
        var secretName = $"backend-{keyId}";
        var now = timeProvider.GetUtcNow();
        var secretWritten = false;
        try
        {
            var secretUtf8 = Encoding.UTF8.GetBytes(secret);
            try
            {
                await secretWriter.WriteSecretAsync(secretName, secretUtf8, cancellationToken);
                secretWritten = true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretUtf8);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var existing = await dbContext.BackendCredentials
                .Where(credential => credential.ApiId == ApiId
                    && credential.Status != nameof(GatewayClientKeyStatus.Revoked))
                .ToListAsync(cancellationToken);

            foreach (var credential in existing)
            {
                if (credential.Status == nameof(GatewayClientKeyStatus.Previous))
                {
                    credential.Status = nameof(GatewayClientKeyStatus.Revoked);
                    credential.RevokedAt = now;
                    credential.PreviousSince = null;
                }
                else if (credential.Status == nameof(GatewayClientKeyStatus.Current))
                {
                    credential.Status = nameof(GatewayClientKeyStatus.Previous);
                    credential.PreviousSince = now;
                    credential.ExpiresAt = now.AddHours(1);
                }

                credential.Version++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.BackendCredentials.Add(new GatewayBackendCredentialEntity
            {
                KeyId = keyId,
                ApiId = ApiId,
                Verifier = verifier,
                PepperVersion = pepperVersion,
                SecretName = secretName,
                Status = nameof(GatewayClientKeyStatus.Current),
                NotBefore = now,
                Version = 1
            });
            dbContext.AuditEvents.Add(CreateAuditEvent(
                issuer,
                subject,
                clientId,
                "backend-credential.issue",
                keyId,
                now));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new IssuedBackendCredential(keyId, $"{keyId}.{secret}", now, null);
        }
        catch (Exception exception)
        {
            if (secretWritten)
            {
                try
                {
                    await secretWriter.DeleteSecretAsync(secretName, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Credential issuance failed and its unreferenced secret could not be removed.",
                        exception,
                        cleanupException);
                }
            }

            if (IsPersistenceFailure(exception))
            {
                throw new BackendCredentialManagementUnavailableException(
                    "The gateway credential store is unavailable.",
                    exception);
            }

            if (IsSecretStorageFailure(exception))
            {
                throw new BackendCredentialManagementUnavailableException(
                    "The backend credential secret provider is unavailable.",
                    exception);
            }

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pepper);
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(verifier);
        }
    }

    public async Task<bool> RevokeAsync(
        string keyId,
        ClaimsPrincipal administrator,
        CancellationToken cancellationToken)
    {
        var (issuer, subject, clientId) = GetAdministratorIdentity(administrator);
        var now = timeProvider.GetUtcNow();
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var credential = await dbContext.BackendCredentials
                .SingleOrDefaultAsync(
                    item => item.ApiId == ApiId && item.KeyId == keyId,
                    cancellationToken);
            if (credential is null || credential.Status == nameof(GatewayClientKeyStatus.Revoked))
            {
                if (credential is null)
                {
                    return false;
                }
            }
            else
            {
                credential.Status = nameof(GatewayClientKeyStatus.Revoked);
                credential.RevokedAt = now;
                credential.PreviousSince = null;
                credential.Version++;
                dbContext.AuditEvents.Add(CreateAuditEvent(
                    issuer,
                    subject,
                    clientId,
                    "backend-credential.revoke",
                    keyId,
                    now));
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await secretWriter.DeleteSecretAsync(credential.SecretName, cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            throw new BackendCredentialManagementUnavailableException(
                "The gateway credential store is unavailable.",
                exception);
        }
        catch (Exception exception) when (IsSecretStorageFailure(exception))
        {
            throw new BackendCredentialManagementUnavailableException(
                "The backend credential secret provider is unavailable.",
                exception);
        }
    }

    private static GatewayAuditEventEntity CreateAuditEvent(
        string issuer,
        string subject,
        string? clientId,
        string action,
        string target,
        DateTimeOffset occurredAt) =>
        new()
        {
            OccurredAt = occurredAt,
            AdminIssuer = issuer,
            AdminSubject = subject,
            AdminClientId = clientId,
            Action = action,
            Target = target,
            Outcome = "succeeded"
        };

    private static (string Issuer, string Subject, string? ClientId) GetAdministratorIdentity(
        ClaimsPrincipal administrator)
    {
        var issuer = administrator.FindFirst("iss")?.Value;
        var subject = administrator.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("The administrator token is missing issuer or subject claims.");
        }

        return (issuer, subject, administrator.FindFirst("client_id")?.Value ?? administrator.FindFirst("azp")?.Value);
    }

    private static bool IsPersistenceFailure(Exception exception) =>
        exception is DbException or DbUpdateException or TimeoutException;

    private static bool IsSecretStorageFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or PlatformNotSupportedException;
}
