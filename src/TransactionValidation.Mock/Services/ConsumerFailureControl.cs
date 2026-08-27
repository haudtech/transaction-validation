using System.Collections.Concurrent;

namespace TransactionValidation.Mock.Services;

public sealed class ConsumerFailureControl
{
    private readonly ConcurrentDictionary<string, byte> _messageIds = new(StringComparer.Ordinal);

    public void FailBeforeAcknowledgement(string consumerName, string messageId)
    {
        _messageIds[$"{consumerName}:{messageId}"] = 0;
    }

    public bool ShouldFailBeforeAcknowledgement(string consumerName, string messageId)
    {
        return _messageIds.TryRemove($"{consumerName}:{messageId}", out _);
    }
}
