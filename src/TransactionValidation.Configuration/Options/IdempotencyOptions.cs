namespace TransactionValidation.Configuration.Options;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public int WindowMinutes { get; set; } = 15;
}