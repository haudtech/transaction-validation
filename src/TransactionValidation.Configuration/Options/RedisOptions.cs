namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Optional Azure Cache for Redis connection used for a distributed idempotency store.
/// When <see cref="ConnectionString"/> is empty the API falls back to an in-memory store (single-replica only).
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
}
