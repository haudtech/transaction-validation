namespace TransactionValidation.Core.Models;

/// <summary>
/// Represents the JSON payload submitted to POST /api/v1/partner/transactions.
/// It contains the partner identifier, transaction metadata, and amount/currency details validated by the BFF.
/// </summary>
public sealed class PartnerTransactionRequest
{
    public required string PartnerId { get; init; }

    public required string TransactionReference { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required DateTime Timestamp { get; init; }
}
