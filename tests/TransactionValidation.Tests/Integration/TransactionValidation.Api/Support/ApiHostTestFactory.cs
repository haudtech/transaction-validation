using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TransactionValidation.Api.Idempotency;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

/// <summary>
/// Configures an in-memory API host with replaceable verifier and publisher doubles for integration tests.
/// </summary>
internal sealed class ApiHostTestFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "integration-test-api-key";

    private readonly IPartnerVerifier _partnerVerifier;
    private readonly IMessagePublisher _messagePublisher;
    private readonly bool _useAzureServiceBus;
    private readonly string? _environmentName;

    public ApiHostTestFactory(
        IPartnerVerifier? partnerVerifier = null,
        IMessagePublisher? messagePublisher = null,
        bool useAzureServiceBus = false,
        string? environmentName = null)
    {
        _partnerVerifier = partnerVerifier ?? new AlwaysVerifiedPartnerVerifier();
        _messagePublisher = messagePublisher ?? new NoOpMessagePublisher();
        _useAzureServiceBus = useAzureServiceBus;
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
                ["Security:Enabled"] = "true",
                ["Security:HeaderName"] = "X-API-Key",
                ["Redis:ConnectionString"] = string.Empty
            };

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

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPartnerVerifier>();
            services.RemoveAll<IMessagePublisher>();
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
            services.RemoveAll<IIdempotencyStore>();

            services.AddSingleton(_partnerVerifier);
            services.AddSingleton(_messagePublisher);
            services.AddSingleton<IIdempotencyStore>(_ =>
                new InMemoryIdempotencyStore(TimeSpan.FromMinutes(15)));
        });
    }
}
