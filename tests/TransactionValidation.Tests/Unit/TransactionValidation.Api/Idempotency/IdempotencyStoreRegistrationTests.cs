using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Idempotency;

public sealed class IdempotencyStoreRegistrationTests
{
    /// <summary>
    /// Scenario: configured idempotency windows are below, within, or above the supported range.
    /// Expected: values are clamped to ten through fifteen minutes.
    /// </summary>
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 10)]
    [InlineData(15, 15)]
    [InlineData(30, 15)]
    public void GetWindow_ClampsConfiguredMinutesToTenThroughFifteen(int configuredMinutes, int expectedMinutes)
    {
        var result = IdempotencyStoreRegistration.GetWindow(new IdempotencyOptions { WindowMinutes = configuredMinutes });

        result.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    /// <summary>
    /// Scenario: the idempotency options argument is null.
    /// Expected: retrieving the window throws an argument-null exception.
    /// </summary>
    [Fact]
    public void GetWindow_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var action = () => IdempotencyStoreRegistration.GetWindow(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Scenario: Redis is not configured.
    /// Expected: the in-memory idempotency store is registered and resolvable.
    /// </summary>
    [Fact]
    public void Add_WhenRedisIsNotConfigured_RegistersInMemoryStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new IdempotencyOptions { WindowMinutes = 12 });
        var configuration = new ConfigurationBuilder().Build();

        IdempotencyStoreRegistration.Add(services, configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyStore>().Should().BeOfType<InMemoryIdempotencyStore>();
    }

    /// <summary>
    /// Scenario: Redis is configured.
    /// Expected: Redis connection and idempotency store registrations are added to the service collection.
    /// </summary>
    [Fact]
    public void Add_WhenRedisIsConfigured_RegistersRedisStoreAndConnection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new IdempotencyOptions { WindowMinutes = 12 });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RedisOptions.SectionName}:ConnectionString"] = "localhost:6379"
            })
            .Build();

        IdempotencyStoreRegistration.Add(services, configuration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IIdempotencyStore));
    }

    /// <summary>
    /// Scenario: Redis is configured and a connection multiplexer is available.
    /// Expected: the registered factory resolves a Redis idempotency store.
    /// </summary>
    [Fact]
    public void Add_WhenRedisIsConfigured_ResolvesRedisStoreWithRegisteredConnection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new IdempotencyOptions { WindowMinutes = 12 });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RedisOptions.SectionName}:ConnectionString"] = "localhost:6379"
            })
            .Build();

        IdempotencyStoreRegistration.Add(services, configuration);
        services.AddSingleton(Moq.Mock.Of<StackExchange.Redis.IConnectionMultiplexer>());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyStore>().Should().BeOfType<RedisIdempotencyStore>();
    }
}
