#nullable enable

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

internal sealed class ApiHostTestFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "integration-test-api-key";

    private readonly IPartnerVerifier _partnerVerifier;
    private readonly IMessagePublisher _messagePublisher;

    public ApiHostTestFactory(IPartnerVerifier? partnerVerifier = null, IMessagePublisher? messagePublisher = null)
    {
        _partnerVerifier = partnerVerifier ?? new AlwaysVerifiedPartnerVerifier();
        _messagePublisher = messagePublisher ?? new NoOpMessagePublisher();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = ApiKey,
                ["Security:Enabled"] = "true",
                ["Security:HeaderName"] = "X-API-Key"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPartnerVerifier>();
            services.RemoveAll<IMessagePublisher>();

            services.AddSingleton(_partnerVerifier);
            services.AddSingleton(_messagePublisher);
        });
    }
}
