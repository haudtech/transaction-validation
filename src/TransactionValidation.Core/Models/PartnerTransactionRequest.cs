namespace TransactionValidation.Core.Models;

public sealed class PartnerTransactionRequest
{
    public required string PartnerId { get; init; }

    public required string TransactionReference { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required DateTime Timestamp { get; init; }
}
