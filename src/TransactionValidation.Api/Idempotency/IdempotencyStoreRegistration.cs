using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Api.Idempotency;

public static class IdempotencyStoreRegistration
{
    public static void Add(IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            services.AddSingleton<IIdempotencyStore>(sp =>
                new InMemoryIdempotencyStore(
                    GetWindow(sp.GetRequiredService<IdempotencyOptions>()),
                    sp.GetRequiredService<ILogger<InMemoryIdempotencyStore>>()));
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisIdempotencyStore(
                connectionMultiplexer,
                GetWindow(sp.GetRequiredService<IdempotencyOptions>()),
                sp.GetRequiredService<ILogger<RedisIdempotencyStore>>());
        });
    }

    public static TimeSpan GetWindow(IdempotencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return TimeSpan.FromMinutes(Math.Clamp(options.WindowMinutes, 10, 15));
    }
}
