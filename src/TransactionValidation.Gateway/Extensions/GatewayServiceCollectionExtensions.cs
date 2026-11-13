using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using TransactionValidation.Gateway.Authentication;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Limiting;
using TransactionValidation.Gateway.Options;
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
        services.AddGatewayRequestLimiting(configuration);
        return services;
    }

    public static IServiceCollection AddGatewayRequestLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GatewayRedisOptions>()
            .Configure(options =>
            {
                options.ConnectionString = configuration.GetConnectionString(GatewayRedisOptions.ConnectionStringName)
                    ?? string.Empty;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "A ConnectionStrings:GatewayRedis value is required.")
            .ValidateOnStart();
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = ConfigurationOptions.Parse(
                serviceProvider.GetRequiredService<IOptions<GatewayRedisOptions>>().Value.ConnectionString);
            // Redis outages are handled per request (fail closed), not at startup.
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IClientRequestLimiter, RedisClientRequestLimiter>();
        return services;
    }
}
