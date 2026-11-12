namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayAuditEventEntity
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public required string AdminIssuer { get; set; }

    public required string AdminSubject { get; set; }

    public string? AdminClientId { get; set; }

    public required string Action { get; set; }

    public required string Target { get; set; }

    public required string Outcome { get; set; }
}
