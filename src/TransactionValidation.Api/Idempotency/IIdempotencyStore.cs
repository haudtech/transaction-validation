namespace TransactionValidation.Api.Idempotency;

/// <summary>
/// Describes whether a request was newly accepted, replayed as a duplicate, or rejected because the same idempotency key was reused with different payload data.
/// This contract supports the duplicate-retry safeguards described in the solution analysis and architecture design.
/// </summary>
public enum IdempotencyAcquireResult
{
    Acquired = 0,
    Duplicate = 1,
    KeyReusedWithDifferentPayload = 2
}

/// <summary>
/// Represents the accepted response state cached for an idempotent transaction replay.
/// </summary>
public enum IdempotencyCachedResponseStatus
{
    Accepted = 0
}

/// <summary>
/// Cached metadata that is returned when the same transaction is replayed within the configured idempotency window.
/// </summary>
public sealed record IdempotencyCachedResponse(
    string MessageId,
    string CorrelationId,
    IdempotencyCachedResponseStatus Status);

/// <summary>
/// Stores transient idempotency entries for in-flight or recently accepted transaction submissions.
/// This component is part of the BFF's replay protection described in docs/analysis/solution_analysis.md.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to reserve a request slot for the supplied idempotency key and request fingerprint.
    /// </summary>
    /// <param name="key">The logical request key used to correlate retries.</param>
    /// <param name="requestFingerprint">Canonical hash of the payload used to detect duplicate payloads.</param>
    /// <param name="nowUtc">Current UTC time used for expiry evaluation.</param>
    /// <returns>The acquisition state: acquired, duplicate, or key reused with different content.</returns>
    IdempotencyAcquireResult TryAcquire(string key, string requestFingerprint, DateTimeOffset nowUtc);

    /// <summary>
    /// Returns the cached accepted response for a previously processed request when the same payload arrives again within the TTL window.
    /// </summary>
    /// <param name="key">The logical idempotency key.</param>
    /// <param name="requestFingerprint">Stable hash of the request payload.</param>
    /// <param name="nowUtc">Current UTC timestamp used to evaluate expiry.</param>
    /// <param name="cachedResponse">The accepted response returned for a replayed request.</param>
    /// <returns>True when a valid cached response exists; otherwise false.</returns>
    bool TryGetCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, out IdempotencyCachedResponse cachedResponse);

    /// <summary>
    /// Stores the accepted response for a successful request so the same payload can be replayed without duplicate processing.
    /// </summary>
    /// <param name="key">The logical idempotency key.</param>
    /// <param name="requestFingerprint">Canonical digest of the request payload.</param>
    /// <param name="nowUtc">Current UTC time used to set the TTL window.</param>
    /// <param name="cachedResponse">The accepted response to replay to callers.</param>
    void StoreCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, IdempotencyCachedResponse cachedResponse);

    /// <summary>
    /// Releases the active idempotency slot when a request fails and must not remain reserved.
    /// </summary>
    /// <param name="key">The request key to release.</param>
    void Release(string key);
}
