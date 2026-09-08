using FluentAssertions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using StackExchange.Redis;

using TransactionValidation.Api.HealthChecks;
using TransactionValidation.Core.Interfaces;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.HealthChecks;

public sealed class HealthChecksTests
{
    [Fact]
    public async Task MessagingHealthCheck_WhenPublisherResolves_ReturnsHealthy()
    {
        var publisher = Moq.Mock.Of<IMessagePublisher>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IMessagePublisher)))
            .Returns(publisher);

        var result = await new MessagingHealthCheck(serviceProvider.Object)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("registered");
    }

    [Fact]
    public async Task MessagingHealthCheck_WhenPublisherDoesNotResolve_ReturnsUnhealthy()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IMessagePublisher)))
            .Returns((object?)null);

        var result = await new MessagingHealthCheck(serviceProvider.Object)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("could not be resolved");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task RedisHealthCheck_WhenRedisIsNotConfigured_ReturnsHealthy()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IConnectionMultiplexer)))
            .Returns((object?)null);

        var result = await new RedisHealthCheck(serviceProvider.Object)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("in-memory");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenRedisPingSucceeds_ReturnsHealthy()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(value => value.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IConnectionMultiplexer)))
            .Returns(multiplexer.Object);

        var result = await new RedisHealthCheck(serviceProvider.Object)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("reachable");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenRedisPingFails_ReturnsUnhealthy()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(value => value.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToResolvePhysicalConnection, "test failure"));

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IConnectionMultiplexer)))
            .Returns(multiplexer.Object);

        var result = await new RedisHealthCheck(serviceProvider.Object)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unreachable");
        result.Exception.Should().BeOfType<RedisConnectionException>();
    }
}
