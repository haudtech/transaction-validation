using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Interfaces;

/// <summary>
/// Contract for publishing a validated transaction envelope after partner verification succeeds.
/// This abstraction supports the message queue workflow described in the architecture design.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default);
}
