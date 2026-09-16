namespace TransactionValidation.Core.Models;

/// <summary>
/// Common non-sensitive fields carried by transaction workflow log events.
/// </summary>
public sealed record TransactionLogContext(
    string CorrelationId,
    string PartnerId,
    string TransactionReference,
    string IdempotencyKey);