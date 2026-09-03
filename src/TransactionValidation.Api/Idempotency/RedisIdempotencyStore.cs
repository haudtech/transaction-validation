using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using StackExchange.Redis;

namespace TransactionValidation.Api.Idempotency;

/// <summary>
/// Redis-backed implementation of the idempotency contract so acquisition and cached-response state
/// are shared across API replicas, unlike <see cref="InMemoryIdempotencyStore"/>.
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private const string KeyPrefix = "idempotency:";

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly TimeSpan _ttl;

    public RedisIdempotencyStore(IConnectionMultiplexer connectionMultiplexer, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Idempotency TTL must be greater than zero.");
        }

        _connectionMultiplexer = connectionMultiplexer;
        _ttl = ttl;
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
            return IdempotencyAcquireResult.Acquired;
        }

        var existingFingerprint = db.StringGet(fingerprintKey);
        return existingFingerprint.HasValue && existingFingerprint.ToString() == normalizedFingerprint
            ? IdempotencyAcquireResult.Duplicate
            : IdempotencyAcquireResult.KeyReusedWithDifferentPayload;
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
