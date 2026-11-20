using System.Net;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using StackExchange.Redis;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayHealthTests
{
    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(true, true, HttpStatusCode.OK)]
    [InlineData(false, true, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, false, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, false, HttpStatusCode.ServiceUnavailable)]
    public async Task Probes_WithoutCredentials_ReportDependencyState(
        bool policyHealthy, bool redisHealthy, HttpStatusCode expected)
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(CreateRegistration("gateway-policy", policyHealthy));
                options.Registrations.Add(CreateRegistration("gateway-redis", redisHealthy));
            });
        }));
        using var client = host.CreateClient();

        using var live = await client.GetAsync("/health/live");
        using var legacy = await client.GetAsync("/healthz");
        using var ready = await client.GetAsync("/health/ready");
        using var nonProbe = await client.GetAsync("/health/ready/other");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Equal(expected, ready.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, nonProbe.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task Probes_WhenPolicyStoreIsUnavailable_ReportUnreadyWithoutInitializingRedisForLiveness()
    {
        var database = new Mock<IDatabase>();
        database.Setup(value => value.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.Zero);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var initialized = false;
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                initialized = true;
                return multiplexer.Object;
            });
        }));
        using var client = host.CreateClient();

        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.False(initialized);

        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.True(initialized);
        database.Verify(value => value.PingAsync(It.IsAny<CommandFlags>()), Times.Once);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task Ready_WhenRedisPingFails_ReturnsServiceUnavailable()
    {
        var database = new Mock<IDatabase>();
        database.Setup(value => value.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "unavailable"));
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(multiplexer.Object);
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var policy = options.Registrations.Single(registration => registration.Name == "gateway-policy");
                options.Registrations.Remove(policy);
                options.Registrations.Add(CreateRegistration("gateway-policy", true));
            });
        }));
        using var client = host.CreateClient();

        using var ready = await client.GetAsync("/health/ready");
        using var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        database.Verify(value => value.PingAsync(It.IsAny<CommandFlags>()), Times.Once);
    }

    private static HealthCheckRegistration CreateRegistration(string name, bool healthy) =>
        new(name, _ => new FixedHealthCheck(healthy), null, ["ready"]);

    private sealed class FixedHealthCheck(bool healthy) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
    }
}
