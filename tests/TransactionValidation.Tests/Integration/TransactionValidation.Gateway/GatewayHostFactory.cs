using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Hosting;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

internal sealed class GatewayHostFactory(
    string destinationAddress,
    string? activityTimeout = null,
    IClientCredentialStore? credentialStore = null,
    ISecretProvider? secretProvider = null,
    IBackendCredentialStore? backendCredentialStore = null,
    IClientRequestLimiter? requestLimiter = null) : WebApplicationFactory<GatewayEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:GatewayPolicy"] = "Host=127.0.0.1;Database=gateway_test;Username=test",
                ["ConnectionStrings:GatewayRedis"] = "127.0.0.1:1,abortConnect=false",
                ["Secrets:Provider"] = "LocalFile",
                ["Secrets:Directory"] = ".secrets",
                ["ReverseProxy:Clusters:transactionValidation:Destinations:api:Address"] = destinationAddress
            };
            if (activityTimeout is not null)
            {
                values["ReverseProxy:Clusters:transactionValidation:HttpRequest:ActivityTimeout"] = activityTimeout;
            }

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClientCredentialStore>();
            services.AddSingleton(credentialStore ?? GatewayTestCredentials.CreateStore());
            services.RemoveAll<ISecretProvider>();
            services.AddSingleton(secretProvider ?? GatewayTestCredentials.CreateSecretProvider());
            services.RemoveAll<IBackendCredentialStore>();
            services.AddSingleton(backendCredentialStore ?? GatewayTestCredentials.CreateBackendCredentialStore());
            services.RemoveAll<IClientRequestLimiter>();
            services.AddSingleton(requestLimiter ?? new InMemoryClientRequestLimiter());
        });
    }
}

internal static class GatewayTestCredentials
{
    public const string KeyId = "test-key";
    public static readonly string ClientSecret = new('A', 43);
    public static string Authorization => $"ApiKey {KeyId}.{ClientSecret}";
    public const string ClientId = "test-client";
    public const string PepperVersion = "pepper-v1";
    public const string BackendCredentialKeyId = "backend-test-key";
    public static readonly string BackendCredentialSecret = new('B', 43);
    public static string BackendCredential => $"{BackendCredentialKeyId}.{BackendCredentialSecret}";
    public static readonly ClientApiLimit DefaultLimit = new("transaction-validation", 600, 100, 100_000, 2_000_000);
    private static readonly byte[] Pepper = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    public static ClientCredentialRecord CreateCredential(
        string keyId = KeyId,
        GatewayClientKeyStatus status = GatewayClientKeyStatus.Current,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null,
        DateTimeOffset? previousSince = null,
        bool clientEnabled = true,
        IReadOnlyList<ClientApiGrant>? grants = null,
        byte[]? verifier = null,
        IReadOnlyList<ClientApiLimit>? limits = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClientCredentialRecord(
            keyId,
            ClientId,
            verifier ?? HMACSHA256.HashData(Pepper, Encoding.ASCII.GetBytes(ClientSecret)),
            PepperVersion,
            status,
            notBefore ?? now.AddMinutes(-1),
            expiresAt,
            revokedAt,
            previousSince,
            clientEnabled,
            grants ?? [new ClientApiGrant("transaction-validation", "v1")],
            limits ?? [DefaultLimit]);
    }

    public static InMemoryClientCredentialStore CreateStore() =>
        new([CreateCredential()]);

    public static InMemorySecretProvider CreateSecretProvider(bool includeBackendCredential = true)
    {
        var secrets = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [PepperVersion] = Pepper
        };
        if (includeBackendCredential)
        {
            secrets[BackendCredentialKeyId] = Encoding.UTF8.GetBytes(BackendCredentialSecret);
        }

        return new InMemorySecretProvider(secrets);
    }

    public static InMemoryBackendCredentialStore CreateBackendCredentialStore(bool includeCredential = true) =>
        new(includeCredential
            ? new BackendCredentialSecretReference(BackendCredentialKeyId, BackendCredentialKeyId)
            : null);
}

internal sealed class InMemoryClientCredentialStore(IEnumerable<ClientCredentialRecord> credentials)
    : IClientCredentialStore
{
    private readonly Dictionary<string, ClientCredentialRecord> records =
        credentials.ToDictionary(credential => credential.KeyId, StringComparer.Ordinal);

    public Task<ClientCredentialRecord?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.TryGetValue(keyId, out var credential);
        return Task.FromResult(credential);
    }
}

internal sealed class InMemorySecretProvider(IReadOnlyDictionary<string, byte[]> secrets) : ISecretProvider
{
    public ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!secrets.TryGetValue(secretName, out var secret))
        {
            throw new FileNotFoundException("The requested test secret was not found.", secretName);
        }

        return ValueTask.FromResult(secret.ToArray());
    }
}

internal sealed class InMemoryBackendCredentialStore(BackendCredentialSecretReference? credential)
    : IBackendCredentialStore
{
    public Task<BackendCredentialSecretReference?> GetCurrentAsync(
        string apiId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(credential);
    }

    public Task<BackendCredentialRecord?> GetByKeyIdAsync(
        string apiId,
        string keyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<BackendCredentialRecord?>(null);
    }
}

/// <summary>Enforcing in-memory limiter with the same decision contract as the Redis implementation.</summary>
internal sealed class InMemoryClientRequestLimiter : IClientRequestLimiter
{
    private readonly Dictionary<string, long> counts = new(StringComparer.Ordinal);

    public Task<RequestLimitDecision> TryAcquireAsync(
        string clientId,
        ClientApiLimit limit,
        CancellationToken cancellationToken)
    {
        lock (counts)
        {
            var key = $"{clientId}|{limit.ApiId}";
            counts.TryGetValue(key, out var count);
            if (count >= limit.DailyQuota)
            {
                return Task.FromResult(new RequestLimitDecision(RequestLimitDenial.DailyQuota, TimeSpan.FromHours(3)));
            }

            if (count >= limit.Burst)
            {
                return Task.FromResult(new RequestLimitDecision(RequestLimitDenial.RateLimit, TimeSpan.FromMilliseconds(6100)));
            }

            counts[key] = count + 1;
            return Task.FromResult(RequestLimitDecision.Allow);
        }
    }
}

internal sealed class UnavailableClientRequestLimiter : IClientRequestLimiter
{
    public Task<RequestLimitDecision> TryAcquireAsync(
        string clientId,
        ClientApiLimit limit,
        CancellationToken cancellationToken) =>
        throw new GatewayRequestLimiterUnavailableException("Redis is down.", new InvalidOperationException());
}
