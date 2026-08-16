namespace TransactionValidation.Api.Idempotency;

public enum IdempotencyAcquireResult
{
    Acquired = 0,
    Duplicate = 1,
    KeyReusedWithDifferentPayload = 2
}

public enum IdempotencyCachedResponseStatus
{
    Accepted = 0
}

public sealed record IdempotencyCachedResponse(
    string MessageId,
    string CorrelationId,
    IdempotencyCachedResponseStatus Status);

public interface IIdempotencyStore
{
    IdempotencyAcquireResult TryAcquire(string key, string requestFingerprint, DateTimeOffset nowUtc);

    bool TryGetCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, out IdempotencyCachedResponse cachedResponse);

    void StoreCachedResponse(string key, string requestFingerprint, DateTimeOffset nowUtc, IdempotencyCachedResponse cachedResponse);

    void Release(string key);
}