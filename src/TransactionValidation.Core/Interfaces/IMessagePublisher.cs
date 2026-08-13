using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default);
}
