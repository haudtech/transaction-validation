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
    /// <summary>
    /// Scenario: the message publisher resolves from the service provider.
    /// Expected: the messaging health check reports healthy.
    /// </summary>
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

    /// <summary>
    /// Scenario: the message publisher cannot be resolved.
    /// Expected: the messaging health check reports unhealthy with an exception.
    /// </summary>
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

    /// <summary>
    /// Scenario: no Redis connection is configured.
    /// Expected: the Redis health check reports healthy using in-memory mode.
    /// </summary>
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

    /// <summary>
    /// Scenario: Redis responds successfully to a ping.
    /// Expected: the Redis health check reports healthy and reachable.
    /// </summary>
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

    /// <summary>
    /// Scenario: the Redis ping throws a connection exception.
    /// Expected: the Redis health check reports unhealthy and preserves the exception.
    /// </summary>
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
