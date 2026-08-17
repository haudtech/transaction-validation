namespace TransactionValidation.Core.Models;

/// <summary>
/// Internal queue message payload produced after a request passes validation and partner verification.
/// It wraps the original transaction with correlation metadata and verification status for downstream processing.
/// </summary>
public sealed class TransactionEnvelope
{
    public required string MessageId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required PartnerTransactionRequest Transaction { get; init; }

    public bool PartnerVerified { get; init; }
}
