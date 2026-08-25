using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

/// <summary>
/// Resolves the broker routing key for a transaction envelope.
/// </summary>
public interface IMessageRoutingKeyResolver
{
    /// <summary>
    /// Gets the routing key for the supplied transaction envelope.
    /// </summary>
    /// <param name="envelope">The transaction envelope to route.</param>
    /// <returns>The broker routing key.</returns>
    string Resolve(TransactionEnvelope envelope);
}
