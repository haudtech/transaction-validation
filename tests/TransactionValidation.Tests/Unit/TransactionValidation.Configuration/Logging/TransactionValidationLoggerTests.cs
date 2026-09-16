using FluentAssertions;

using Microsoft.Extensions.Logging;

using TransactionValidation.Core.Logging;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration.Logging;

public sealed class TransactionValidationLoggerTests
{
    [Fact]
    public void GeneratedEvents_UseApprovedStructuredPropertiesOnly()
    {
        var logger = new CapturingLogger();
        var exception = new InvalidOperationException("test");

        var events = new Action[]
        {
            () => TransactionValidationLogger.RequestReceived(logger, "corr-123", "partner-123", "ref-001"),
            () => TransactionValidationLogger.ValidationFailed(logger, "corr-123", "partner-123", "ref-001", "invalid"),
            () => TransactionValidationLogger.DuplicateReplayed(logger, "corr-123", "partner-123", "ref-001", "msg-123"),
            () => TransactionValidationLogger.IdempotencyConflict(logger, "corr-123", "partner-123", "ref-001"),
            () => TransactionValidationLogger.PartnerVerificationCompleted(logger, "corr-123", "partner-123", "ref-001", 12.5),
            () => TransactionValidationLogger.TransactionPublished(logger, "corr-123", "partner-123", "ref-001", "msg-123", 12.5),
            () => TransactionValidationLogger.ProcessingFailed(logger, exception, "corr-123", "partner-123", "ref-001", 12.5)
        };

        foreach (var logEvent in events)
        {
            logEvent();

            logger.EventId.Id.Should().BeInRange(1000, 1006);
            logger.Properties.Keys.Should().BeSubsetOf(
                new[]
                {
                    "CorrelationId",
                    "PartnerId",
                    "TransactionReference",
                    "MessageId",
                    "Reason",
                    "DurationMs",
                    "{OriginalFormat}"
                });
            logger.Properties.Keys.Should().NotContain("IdempotencyKey");
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public EventId EventId { get; private set; }

        public IReadOnlyDictionary<string, object?> Properties { get; private set; } = new Dictionary<string, object?>();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            EventId = eventId;
            Properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
