namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Runtime settings for the duplicate-request replay window used by the in-memory idempotency store.
/// These values support the idempotency behavior recommended in the design and analysis docs.
/// </summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public int WindowMinutes { get; set; } = 15;
}