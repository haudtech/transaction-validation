using System.Collections.Concurrent;

namespace TransactionValidation.Mock.Services;

public sealed class ConsumerObservationStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConsumerObservation>> _observations = new(StringComparer.Ordinal);

    public void Add(ConsumerObservation observation)
    {
        _observations.GetOrAdd(observation.ConsumerName, _ => new ConcurrentQueue<ConsumerObservation>()).Enqueue(observation);
    }

    public IReadOnlyCollection<ConsumerObservation> Get(string consumerName)
    {
        return _observations.TryGetValue(consumerName, out var observations)
            ? observations.ToArray()
            : Array.Empty<ConsumerObservation>();
    }
}

public sealed record ConsumerObservation(
    string ConsumerName,
    string QueueName,
    string MessageId,
    string CorrelationId,
    string RoutingKey,
    bool Redelivered,
    int DeliveryAttempt,
    DateTimeOffset ObservedAt);
