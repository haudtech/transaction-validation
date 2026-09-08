using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Idempotency;

public sealed class IdempotencyStoreRegistrationTests
{
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

    [Fact]
    public void GetWindow_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var action = () => IdempotencyStoreRegistration.GetWindow(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_WhenRedisIsNotConfigured_RegistersInMemoryStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new IdempotencyOptions { WindowMinutes = 12 });
        var configuration = new ConfigurationBuilder().Build();

        IdempotencyStoreRegistration.Add(services, configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyStore>().Should().BeOfType<InMemoryIdempotencyStore>();
    }

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
}
