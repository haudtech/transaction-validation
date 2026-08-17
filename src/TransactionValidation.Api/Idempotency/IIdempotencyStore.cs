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
    IdempotencyAcquireResult TryAcquire(string key, string requestFingerprint, DateTimeOffset nowUtc);

    bool TryGetCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, out IdempotencyCachedResponse cachedResponse);

    void StoreCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, IdempotencyCachedResponse cachedResponse);

    void Release(string key);
}