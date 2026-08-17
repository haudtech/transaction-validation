using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TransactionValidation.Api.Idempotency;

/// <summary>
/// In-memory implementation of the idempotency contract used to prevent duplicate partner submissions within a short TTL.
/// It aligns with the recommendation in the design docs to replay cached accepted responses for identical requests while rejecting key reuse with different payloads.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private int _cleanupCounter;

    private readonly record struct IdempotencyEntry(DateTimeOffset ExpiresAt, string RequestFingerprint, IdempotencyCachedResponse? CachedResponse);

    public InMemoryIdempotencyStore(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Idempotency TTL must be greater than zero.");
        }

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

        var encodedKey = EncodeKey(key);

        CleanupExpiredEntriesIfNeeded(nowUtc);

        var expiresAt = nowUtc.Add(_ttl);
        var normalizedFingerprint = requestFingerprint.Trim();

        while (true)
        {
            if (_entries.TryGetValue(encodedKey, out var existingEntry))
            {
                if (existingEntry.ExpiresAt <= nowUtc)
                {
                    _entries.TryRemove(new KeyValuePair<string, IdempotencyEntry>(encodedKey, existingEntry));
                    continue;
                }

                return string.Equals(existingEntry.RequestFingerprint, normalizedFingerprint, StringComparison.Ordinal)
                    ? IdempotencyAcquireResult.Duplicate
                    : IdempotencyAcquireResult.KeyReusedWithDifferentPayload;
            }

            if (_entries.TryAdd(encodedKey, new IdempotencyEntry(expiresAt, normalizedFingerprint, null)))
            {
                return IdempotencyAcquireResult.Acquired;
            }
        }
    }

    public bool TryGetCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, out IdempotencyCachedResponse cachedResponse)
    {
        cachedResponse = null!;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(requestFingerprint))
        {
            return false;
        }

        var encodedKey = EncodeKey(key);
        var normalizedFingerprint = requestFingerprint.Trim();

        if (!_entries.TryGetValue(encodedKey, out var existingEntry))
        {
            return false;
        }

        if (existingEntry.ExpiresAt <= nowUtc)
        {
            _entries.TryRemove(new KeyValuePair<string, IdempotencyEntry>(encodedKey, existingEntry));
            return false;
        }

        if (!string.Equals(existingEntry.RequestFingerprint, normalizedFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (existingEntry.CachedResponse is null)
        {
            return false;
        }

        cachedResponse = existingEntry.CachedResponse;
        return true;
    }

    public void StoreCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, IdempotencyCachedResponse cachedResponse)
    {
        if (string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(requestFingerprint)
            || cachedResponse is null)
        {
            return;
        }

        var encodedKey = EncodeKey(key);
        var normalizedFingerprint = requestFingerprint.Trim();
        var expiresAt = nowUtc.Add(_ttl);

        while (true)
        {
            if (_entries.TryGetValue(encodedKey, out var existingEntry))
            {
                if (existingEntry.ExpiresAt <= nowUtc)
                {
                    _entries.TryRemove(new KeyValuePair<string, IdempotencyEntry>(encodedKey, existingEntry));
                    continue;
                }

                if (!string.Equals(existingEntry.RequestFingerprint, normalizedFingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                var updatedEntry = existingEntry with
                {
                    ExpiresAt = expiresAt,
                    CachedResponse = cachedResponse
                };

                if (_entries.TryUpdate(encodedKey, updatedEntry, existingEntry))
                {
                    return;
                }

                continue;
            }

            if (_entries.TryAdd(encodedKey, new IdempotencyEntry(expiresAt, normalizedFingerprint, cachedResponse)))
            {
                return;
            }
        }
    }

    public void Release(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var encodedKey = EncodeKey(key);
        _entries.TryRemove(encodedKey, out _);
    }

    private static string EncodeKey(string key)
    {
        var normalized = key.Trim();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private void CleanupExpiredEntriesIfNeeded(DateTimeOffset nowUtc)
    {
        if (Interlocked.Increment(ref _cleanupCounter) % 128 != 0)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (entry.Value.ExpiresAt <= nowUtc)
            {
                _entries.TryRemove(new KeyValuePair<string, IdempotencyEntry>(entry.Key, entry.Value));
            }
        }
    }
}