namespace TransactionValidation.Core.Models;

public sealed class TransactionEnvelope
{
    public required string MessageId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required PartnerTransactionRequest Transaction { get; init; }

    public bool PartnerVerified { get; init; }
}
