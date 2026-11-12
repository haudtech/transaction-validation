using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using TransactionValidation.Gateway.Contracts;

namespace TransactionValidation.Gateway.PolicyStore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayPolicyStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GatewayPolicyStoreOptions>()
            .Bind(configuration.GetSection(GatewayPolicyStoreOptions.SectionName))
            .Configure(options =>
            {
                options.ConnectionString = configuration.GetConnectionString("GatewayPolicy") ?? string.Empty;
            })
            .Validate(options => IsValidConnectionString(options.ConnectionString),
                "A valid ConnectionStrings:GatewayPolicy value is required.")
            .Validate(options => options.ConnectionTimeoutSeconds is >= 1 and <= 30,
                "Gateway policy-store connection timeout must be between 1 and 30 seconds.")
            .Validate(options => options.CommandTimeoutSeconds is >= 1 and <= 30,
                "Gateway policy-store command timeout must be between 1 and 30 seconds.")
            .ValidateOnStart();

        services.AddDbContext<GatewayPolicyDbContext>((serviceProvider, dbOptions) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GatewayPolicyStoreOptions>>().Value;
            var connectionString = new NpgsqlConnectionStringBuilder(options.ConnectionString)
            {
                Timeout = options.ConnectionTimeoutSeconds
            }.ConnectionString;

            dbOptions.UseNpgsql(
                connectionString,
                postgres => postgres.CommandTimeout(options.CommandTimeoutSeconds));
        });
        services.AddScoped<IClientCredentialStore, EfClientCredentialStore>();

        return services;
    }

    private static bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
