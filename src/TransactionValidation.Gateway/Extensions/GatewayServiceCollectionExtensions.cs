using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TransactionValidation.Gateway.Authentication;
using TransactionValidation.Gateway.PolicyStore.Extensions;
using TransactionValidation.Gateway.Secrets.Extensions;

namespace TransactionValidation.Gateway.Extensions;

public static class GatewayServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddConfiguredSecretProvider(configuration, environment);
        services.AddGatewayPolicyStore(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<GatewayClientAuthenticator>();
        return services;
    }
}
