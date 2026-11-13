using StackExchange.Redis;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Limiting;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class RedisClientRequestLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [RedisFact]
    public async Task TryAcquireAsync_WhenBurstExhausted_DeniesWithRetryAfterAndRefills()
    {
        await using var redis = await ConnectAsync();
        var time = new MutableTimeProvider(Start);
        var limiter = new RedisClientRequestLimiter(redis, time);
        var limit = new ClientApiLimit("api", RatePerMinute: 60, Burst: 2, DailyQuota: 1000, MonthlyQuota: 10_000);
        var client = NewClient();

        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
        var denied = await limiter.TryAcquireAsync(client, limit, CancellationToken.None);

        Assert.Equal(RequestLimitDenial.RateLimit, denied.Denial);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
    }

    [RedisFact]
    public async Task TryAcquireAsync_WhenDailyQuotaExhausted_DeniesUntilUtcMidnightThenResets()
    {
        await using var redis = await ConnectAsync();
        var time = new MutableTimeProvider(Start);
        var limiter = new RedisClientRequestLimiter(redis, time);
        var limit = new ClientApiLimit("api", RatePerMinute: 6000, Burst: 100, DailyQuota: 2, MonthlyQuota: 10);
        var client = NewClient();

        await limiter.TryAcquireAsync(client, limit, CancellationToken.None);
        await limiter.TryAcquireAsync(client, limit, CancellationToken.None);
        var denied = await limiter.TryAcquireAsync(client, limit, CancellationToken.None);

        Assert.Equal(RequestLimitDenial.DailyQuota, denied.Denial);
        Assert.Equal(TimeSpan.FromHours(14), denied.RetryAfter);

        time.Advance(TimeSpan.FromHours(14));
        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
    }

    [RedisFact]
    public async Task TryAcquireAsync_WhenMonthlyQuotaExhausted_DeniesUntilNextMonth()
    {
        await using var redis = await ConnectAsync();
        var time = new MutableTimeProvider(Start);
        var limiter = new RedisClientRequestLimiter(redis, time);
        var limit = new ClientApiLimit("api", RatePerMinute: 6000, Burst: 100, DailyQuota: 1, MonthlyQuota: 2);
        var client = NewClient();

        await limiter.TryAcquireAsync(client, limit, CancellationToken.None);
        time.Advance(TimeSpan.FromDays(1));
        await limiter.TryAcquireAsync(client, limit, CancellationToken.None);
        time.Advance(TimeSpan.FromDays(1));
        var denied = await limiter.TryAcquireAsync(client, limit, CancellationToken.None);

        Assert.Equal(RequestLimitDenial.MonthlyQuota, denied.Denial);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero) - time.GetUtcNow(), denied.RetryAfter);
    }

    [RedisFact]
    public async Task TryAcquireAsync_WhenRateDenied_DoesNotConsumeQuota()
    {
        await using var redis = await ConnectAsync();
        var time = new MutableTimeProvider(Start);
        var limiter = new RedisClientRequestLimiter(redis, time);
        var limit = new ClientApiLimit("api", RatePerMinute: 60, Burst: 1, DailyQuota: 2, MonthlyQuota: 10);
        var client = NewClient();

        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(RequestLimitDenial.RateLimit, (await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Denial);
        }

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Allowed);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(RequestLimitDenial.DailyQuota, (await limiter.TryAcquireAsync(client, limit, CancellationToken.None)).Denial);
    }

    [RedisFact]
    public async Task TryAcquireAsync_UnderConcurrency_NeverAdmitsMoreThanBurst()
    {
        await using var redis = await ConnectAsync();
        var limiter = new RedisClientRequestLimiter(redis, new MutableTimeProvider(Start));
        var limit = new ClientApiLimit("api", RatePerMinute: 60, Burst: 20, DailyQuota: 1000, MonthlyQuota: 10_000);
        var client = NewClient();

        var decisions = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => limiter.TryAcquireAsync(client, limit, CancellationToken.None))));

        Assert.Equal(20, decisions.Count(decision => decision.Allowed));
    }

    [RedisFact]
    public async Task TryAcquireAsync_CountersAreSeparatePerClientAndSharedAcrossVersions()
    {
        await using var redis = await ConnectAsync();
        var limiter = new RedisClientRequestLimiter(redis, new MutableTimeProvider(Start));
        var limit = new ClientApiLimit("api", RatePerMinute: 60, Burst: 1, DailyQuota: 10, MonthlyQuota: 10);
        var first = NewClient();
        var second = NewClient();

        Assert.True((await limiter.TryAcquireAsync(first, limit, CancellationToken.None)).Allowed);
        Assert.False((await limiter.TryAcquireAsync(first, limit, CancellationToken.None)).Allowed);
        Assert.True((await limiter.TryAcquireAsync(second, limit, CancellationToken.None)).Allowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisUnreachable_ThrowsLimiterUnavailable()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=200,syncTimeout=200,asyncTimeout=200");
        var limiter = new RedisClientRequestLimiter(redis, TimeProvider.System);

        await Assert.ThrowsAsync<GatewayRequestLimiterUnavailableException>(() =>
            limiter.TryAcquireAsync("client", new ClientApiLimit("api", 60, 1, 1, 1), CancellationToken.None));
    }

    private static string NewClient() => "client-" + Guid.NewGuid().ToString("N");

    private static Task<ConnectionMultiplexer> ConnectAsync() =>
        ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable(RedisFactAttribute.EnvironmentVariable)!);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current += duration;
    }
}
