using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

/// <summary>
/// Resolves partner transaction outcome routing keys for the RabbitMQ topic exchange.
/// </summary>
public sealed class PartnerTransactionRoutingKeyResolver : IMessageRoutingKeyResolver
{
    private readonly string _routingKeyPrefix;

    /// <summary>
    /// Initializes the resolver with a routing-key prefix.
    /// </summary>
    /// <param name="routingKeyPrefix">Prefix used for partner transaction routing keys.</param>
    public PartnerTransactionRoutingKeyResolver(string routingKeyPrefix)
    {
        _routingKeyPrefix = string.IsNullOrWhiteSpace(routingKeyPrefix)
            ? throw new ArgumentException("Routing-key prefix is required.", nameof(routingKeyPrefix))
            : routingKeyPrefix.Trim();
    }

    /// <summary>
    /// Resolves an accepted or unverified routing key from the envelope status.
    /// </summary>
    /// <param name="envelope">The transaction envelope to route.</param>
    /// <returns>The topic exchange routing key.</returns>
    public string Resolve(TransactionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var outcome = envelope.PartnerVerified ? "accepted" : "unverified";
        return $"{_routingKeyPrefix}.{outcome}";
    }
}
