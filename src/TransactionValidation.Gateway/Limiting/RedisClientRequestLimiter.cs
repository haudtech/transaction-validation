using System.Globalization;

using StackExchange.Redis;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Limiting;

/// <summary>
/// Applies UTC daily/monthly quotas and a token bucket in one atomic Redis script.
/// A request that is denied consumes neither quota nor a token.
/// </summary>
public sealed class RedisClientRequestLimiter(IConnectionMultiplexer redis, TimeProvider timeProvider)
    : IClientRequestLimiter
{
    // Quota keys outlive their window slightly so a late INCR can never resurrect a full counter.
    private const long QuotaKeySlackMilliseconds = 60_000;

    // KEYS: bucket, daily counter, monthly counter.
    // ARGV: now_ms, refill_per_ms, burst, daily_limit, monthly_limit, daily_ttl_ms, monthly_ttl_ms,
    //       daily_retry_ms, monthly_retry_ms. Returns { denial, retry_ms }.
    private const string Script = """
        local now = tonumber(ARGV[1])
        local refill = tonumber(ARGV[2])
        local burst = tonumber(ARGV[3])
        local daily = tonumber(redis.call('GET', KEYS[2]) or '0')
        if daily >= tonumber(ARGV[4]) then return { 2, tonumber(ARGV[8]) } end
        local monthly = tonumber(redis.call('GET', KEYS[3]) or '0')
        if monthly >= tonumber(ARGV[5]) then return { 3, tonumber(ARGV[9]) } end
        local state = redis.call('HMGET', KEYS[1], 'tokens', 'ts')
        local tokens = tonumber(state[1])
        local ts = tonumber(state[2])
        if tokens == nil or ts == nil then
          tokens = burst
          ts = now
        end
        tokens = math.min(burst, tokens + math.max(0, now - ts) * refill)
        if tokens < 1 then
          return { 1, math.ceil((1 - tokens) / refill) }
        end
        redis.call('HSET', KEYS[1], 'tokens', tostring(tokens - 1), 'ts', tostring(now))
        redis.call('PEXPIRE', KEYS[1], math.ceil(burst / refill) + 1000)
        if redis.call('INCR', KEYS[2]) == 1 then redis.call('PEXPIRE', KEYS[2], tonumber(ARGV[6])) end
        if redis.call('INCR', KEYS[3]) == 1 then redis.call('PEXPIRE', KEYS[3], tonumber(ARGV[7])) end
        return { 0, 0 }
        """;

    public async Task<RequestLimitDecision> TryAcquireAsync(
        string clientId,
        ClientApiLimit limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = timeProvider.GetUtcNow();
        var nextDay = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        var firstOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var nextMonth = firstOfMonth.AddMonths(1);
        var dailyRetry = (long)(nextDay - now).TotalMilliseconds;
        var monthlyRetry = (long)(nextMonth - now).TotalMilliseconds;

        var prefix = $"gw:rl:{{{clientId}|{limit.ApiId}}}";
        RedisKey[] keys =
        [
            $"{prefix}:bucket",
            $"{prefix}:d:{now.UtcDateTime:yyyyMMdd}",
            $"{prefix}:m:{now.UtcDateTime:yyyyMM}"
        ];
        RedisValue[] arguments =
        [
            now.ToUnixTimeMilliseconds(),
            (limit.RatePerMinute / 60000d).ToString("R", CultureInfo.InvariantCulture),
            limit.Burst,
            limit.DailyQuota,
            limit.MonthlyQuota,
            dailyRetry + QuotaKeySlackMilliseconds,
            monthlyRetry + QuotaKeySlackMilliseconds,
            dailyRetry,
            monthlyRetry
        ];

        try
        {
            var result = (RedisResult[]?)await redis.GetDatabase()
                .ScriptEvaluateAsync(Script, keys, arguments)
                .WaitAsync(cancellationToken);
            var denial = (RequestLimitDenial)(int)result![0];
            return denial == RequestLimitDenial.None
                ? RequestLimitDecision.Allow
                : new RequestLimitDecision(denial, TimeSpan.FromMilliseconds((long)result[1]));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or ObjectDisposedException)
        {
            throw new GatewayRequestLimiterUnavailableException("The gateway request limiter is unavailable.", exception);
        }
    }
}
