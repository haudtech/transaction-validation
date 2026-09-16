using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using TransactionValidation.Core.Logging;

namespace TransactionValidation.Api.Idempotency;

/// <summary>
/// Redis-backed implementation of the idempotency contract so acquisition and cached-response state
/// are shared across API replicas, unlike <see cref="InMemoryIdempotencyStore"/>.
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private enum CacheOutcome
    {
        Found,
        Stored,
        Released
    }

    private const string DependencyName = nameof(RedisIdempotencyStore);
    private const string KeyPrefix = "idempotency:";

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly TimeSpan _ttl;
    private readonly ILogger<RedisIdempotencyStore> _logger;

    public RedisIdempotencyStore(IConnectionMultiplexer connectionMultiplexer, TimeSpan ttl, ILogger<RedisIdempotencyStore> logger)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Idempotency TTL must be greater than zero.");
        }

        _connectionMultiplexer = connectionMultiplexer;
        _ttl = ttl;
        _logger = logger;
    }

    public IdempotencyAcquireResult TryAcquire(string key, string requestFingerprint, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(requestFingerprint))
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));
        }

        var db = _connectionMultiplexer.GetDatabase();
        var fingerprintKey = FingerprintKey(key);
        var normalizedFingerprint = requestFingerprint.Trim();

        // Atomic reservation: only the first caller for this key within the TTL window wins.
        if (db.StringSet(fingerprintKey, normalizedFingerprint, _ttl, When.NotExists))
        {
            TransactionValidationLogger.IdempotencyAcquisitionCompleted(_logger, DependencyName, nameof(IdempotencyAcquireResult.Acquired));
            return IdempotencyAcquireResult.Acquired;
        }

        var existingFingerprint = db.StringGet(fingerprintKey);
        var result = existingFingerprint.HasValue && existingFingerprint.ToString() == normalizedFingerprint
            ? IdempotencyAcquireResult.Duplicate
            : IdempotencyAcquireResult.KeyReusedWithDifferentPayload;
        TransactionValidationLogger.IdempotencyAcquisitionCompleted(_logger, DependencyName, result.ToString());
        return result;
    }

    public bool TryGetCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, out IdempotencyCachedResponse cachedResponse)
    {
        cachedResponse = null!;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(requestFingerprint))
        {
            return false;
        }

        var db = _connectionMultiplexer.GetDatabase();
        var normalizedFingerprint = requestFingerprint.Trim();

        var existingFingerprint = db.StringGet(FingerprintKey(key));
        if (!existingFingerprint.HasValue || existingFingerprint.ToString() != normalizedFingerprint)
        {
            return false;
        }

        var responseJson = db.StringGet(ResponseKey(key));
        if (!responseJson.HasValue)
        {
            return false;
        }

        cachedResponse = JsonSerializer.Deserialize<IdempotencyCachedResponse>(responseJson!)!;
        TransactionValidationLogger.IdempotencyCacheLookupCompleted(_logger, DependencyName, nameof(CacheOutcome.Found));
        return true;
    }

    public void StoreCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, IdempotencyCachedResponse cachedResponse)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(requestFingerprint) || cachedResponse is null)
        {
            return;
        }

        var db = _connectionMultiplexer.GetDatabase();
        var normalizedFingerprint = requestFingerprint.Trim();
        var fingerprintKey = FingerprintKey(key);

        var existingFingerprint = db.StringGet(fingerprintKey);
        if (existingFingerprint.HasValue && existingFingerprint.ToString() != normalizedFingerprint)
        {
            return;
        }

        db.StringSet(fingerprintKey, normalizedFingerprint, _ttl);
        db.StringSet(ResponseKey(key), JsonSerializer.Serialize(cachedResponse), _ttl);
        TransactionValidationLogger.IdempotencyCacheStorageCompleted(_logger, DependencyName, nameof(CacheOutcome.Stored));
    }

    public void Release(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var db = _connectionMultiplexer.GetDatabase();
        db.KeyDelete(FingerprintKey(key));
        db.KeyDelete(ResponseKey(key));
        TransactionValidationLogger.IdempotencyReleaseCompleted(_logger, DependencyName, nameof(CacheOutcome.Released));
    }

    private static string FingerprintKey(string key) => $"{KeyPrefix}{EncodeKey(key)}:fingerprint";

    private static string ResponseKey(string key) => $"{KeyPrefix}{EncodeKey(key)}:response";

    private static string EncodeKey(string key)
    {
        var normalized = key.Trim();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
