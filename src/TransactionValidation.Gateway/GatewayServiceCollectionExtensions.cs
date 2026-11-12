using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts;
using TransactionValidation.Gateway.PolicyStore.Extensions;
using TransactionValidation.Gateway.Secrets;

namespace TransactionValidation.Gateway;

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
