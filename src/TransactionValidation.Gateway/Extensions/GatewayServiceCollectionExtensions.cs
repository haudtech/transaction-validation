using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Authentication;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Options;
using TransactionValidation.Gateway.PolicyStore.Extensions;
using TransactionValidation.Gateway.Secrets.Providers;
using TransactionValidation.Gateway.Validators;

namespace TransactionValidation.Gateway.Extensions;

public static class GatewayServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<GatewayRuntimeOptions>()
            .Bind(configuration.GetSection(GatewayRuntimeOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GatewayRuntimeOptions>, GatewayRuntimeOptionsValidator>();
        services.AddSingleton<ISecretProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GatewayRuntimeOptions>>().Value;
            var rootDirectory = Path.GetFullPath(options.SecretsDirectory, environment.ContentRootPath);
            return new LocalFileSecretProvider(rootDirectory);
        });
        services.AddGatewayPolicyStore(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<GatewayClientAuthenticator>();

        return services;
    }
}
