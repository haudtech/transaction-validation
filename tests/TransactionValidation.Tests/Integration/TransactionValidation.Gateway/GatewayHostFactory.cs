using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Hosting;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

internal sealed class GatewayHostFactory(
    string destinationAddress,
    string? activityTimeout = null,
    IClientCredentialStore? credentialStore = null,
    ISecretProvider? secretProvider = null) : WebApplicationFactory<GatewayEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:GatewayPolicy"] = "Host=127.0.0.1;Database=gateway_test;Username=test",
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
    public const string BackendCredential = "test-gateway-backend-credential";
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
        byte[]? verifier = null)
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
            grants ?? [new ClientApiGrant("transaction-validation", "v1")]);
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
            secrets["transaction-validation-backend"] = Encoding.UTF8.GetBytes(BackendCredential);
        }

        return new InMemorySecretProvider(secrets);
    }
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
