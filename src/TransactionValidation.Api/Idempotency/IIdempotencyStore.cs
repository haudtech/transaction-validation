namespace TransactionValidation.Api.Idempotency;

public enum IdempotencyAcquireResult
{
    Acquired = 0,
    Duplicate = 1,
    KeyReusedWithDifferentPayload = 2
}

public interface IIdempotencyStore
{
    IdempotencyAcquireResult TryAcquire(string key, string requestFingerprint, DateTimeOffset nowUtc);

    void Release(string key);
}