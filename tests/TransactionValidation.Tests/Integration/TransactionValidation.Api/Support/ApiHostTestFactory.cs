using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

using TransactionValidation.Api;
using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Authentication;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

/// <summary>
/// Configures an in-memory API host with replaceable credential-store, secret-provider, and publisher doubles.
/// </summary>
internal sealed class ApiHostTestFactory : WebApplicationFactory<ApiEntryPoint>
{
    public const string ApiKey = "integration-test-api-key";
    public const string BackendKeyId = "integration-backend-key";
    public static readonly string BackendSecret = new('A', 43);
    public static string GatewayCredential => $"{BackendKeyId}.{BackendSecret}";
    private static readonly byte[] BackendPepper = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    private readonly IPartnerVerifier _partnerVerifier;
    private readonly IMessagePublisher _messagePublisher;
    private readonly bool _useAzureServiceBus;
    private readonly bool _useGatewayAuthentication;
    private readonly string? _environmentName;

    public ApiHostTestFactory(
        IPartnerVerifier? partnerVerifier = null,
        IMessagePublisher? messagePublisher = null,
        bool useAzureServiceBus = false,
        string? environmentName = null,
        bool useGatewayAuthentication = false)
    {
        _partnerVerifier = partnerVerifier ?? new AlwaysVerifiedPartnerVerifier();
        _messagePublisher = messagePublisher ?? new NoOpMessagePublisher();
        _useAzureServiceBus = useAzureServiceBus;
        _useGatewayAuthentication = useGatewayAuthentication;
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(_environmentName))
        {
            builder.UseEnvironment(_environmentName);
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = ApiKey,
                ["Security:AuthenticationMode"] = _useGatewayAuthentication ? "Gateway" : "DirectApiKey",
                ["Security:HeaderName"] = "X-API-Key",
                ["Redis:ConnectionString"] = string.Empty,
                ["ConnectionStrings:GatewayPolicy"] = "Host=127.0.0.1;Database=gateway_test;Username=test"
            };
            if (_useGatewayAuthentication)
            {
                settings["Secrets:Provider"] = "LocalFile";
                settings["Secrets:Directory"] = ".secrets";
            }

            if (_useAzureServiceBus)
            {
                settings["Messaging:BrokerType"] = "AzureServiceBus";
                settings["ServiceBusPublisher:ConnectionString"] = "Endpoint=sb://integration.test/;";
                settings["ServiceBusPublisher:TopicName"] = "partner.transactions";
                settings["ServiceBusPublisher:Subject"] = "partner.transaction.accepted";
                settings["ServiceBusPublisher:RoutingKey"] = "partner.transaction.accepted";
                settings["ServiceBusPublisher:EventType"] = "PartnerTransactionAccepted";
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPartnerVerifier>();
            services.RemoveAll<IMessagePublisher>();
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
            services.RemoveAll<IIdempotencyStore>();

            services.AddSingleton(_partnerVerifier);
            services.AddSingleton(_messagePublisher);
            services.AddSingleton<IIdempotencyStore>(_ =>
                new InMemoryIdempotencyStore(TimeSpan.FromMinutes(15), NullLogger<InMemoryIdempotencyStore>.Instance));

            if (_useGatewayAuthentication)
            {
                services.RemoveAll<IBackendCredentialStore>();
                services.AddSingleton<IBackendCredentialStore>(new InMemoryBackendCredentialStore());
                services.RemoveAll<ISecretProvider>();
                services.AddSingleton<ISecretProvider>(new InMemoryBackendSecretProvider());
                services.RemoveAll<IGatewayCredentialVerifier>();
                services.AddScoped<IGatewayCredentialVerifier, DatabaseGatewayCredentialVerifier>();
                services.AddSingleton(TimeProvider.System);
            }
        });
    }

    private sealed class InMemoryBackendCredentialStore : IBackendCredentialStore
    {
        private readonly BackendCredentialRecord credential = new(
            BackendKeyId,
            "transaction-validation",
            HMACSHA256.HashData(BackendPepper, Encoding.ASCII.GetBytes(BackendSecret)),
            "backend-pepper-v1",
            GatewayClientKeyStatus.Current,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            null,
            null,
            null);

        public Task<BackendCredentialSecretReference?> GetCurrentAsync(
            string apiId,
            CancellationToken cancellationToken) =>
            Task.FromResult<BackendCredentialSecretReference?>(
                apiId == "transaction-validation"
                    ? new BackendCredentialSecretReference(BackendKeyId, "backend-secret")
                    : null);

        public Task<BackendCredentialRecord?> GetByKeyIdAsync(
            string apiId,
            string keyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<BackendCredentialRecord?>(
                apiId == credential.ApiId && keyId == credential.KeyId ? credential : null);
    }

    private sealed class InMemoryBackendSecretProvider : ISecretProvider
    {
        public ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken) =>
            ValueTask.FromResult(BackendPepper.ToArray());
    }
}
