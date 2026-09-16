using Microsoft.Extensions.Logging;

namespace TransactionValidation.Core.Logging;

/// <summary>
/// Common source-generated structured logging events. The caller supplies its typed logger so the log category identifies the owning class.
/// </summary>
public static partial class TransactionValidationLogger
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Transaction request received. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference}")]
    public static partial void RequestReceived(ILogger logger, string correlationId, string partnerId, string transactionReference);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Transaction validation failed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} Reason={Reason}")]
    public static partial void ValidationFailed(ILogger logger, string correlationId, string partnerId, string transactionReference, string reason);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Duplicate transaction response replayed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} MessageId={MessageId}")]
    public static partial void DuplicateReplayed(ILogger logger, string correlationId, string partnerId, string transactionReference, string messageId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Idempotency key reused with a different payload. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference}")]
    public static partial void IdempotencyConflict(ILogger logger, string correlationId, string partnerId, string transactionReference);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Partner verification completed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationCompleted(ILogger logger, string correlationId, string partnerId, string transactionReference, double durationMs);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Transaction published. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} MessageId={MessageId} DurationMs={DurationMs}")]
    public static partial void TransactionPublished(ILogger logger, string correlationId, string partnerId, string transactionReference, string messageId, double durationMs);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Error,
        Message = "Transaction processing failed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} DurationMs={DurationMs}")]
    public static partial void ProcessingFailed(ILogger logger, Exception exception, string correlationId, string partnerId, string transactionReference, double durationMs);

    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Message publish completed. Dependency={Dependency} CorrelationId={CorrelationId} MessageId={MessageId} DurationMs={DurationMs}")]
    public static partial void MessagePublishCompleted(
        ILogger logger,
        string dependency,
        string correlationId,
        string messageId,
        double durationMs);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Error,
        Message = "Message publish failed. Dependency={Dependency} CorrelationId={CorrelationId} MessageId={MessageId} DurationMs={DurationMs}")]
    public static partial void MessagePublishFailed(
        ILogger logger,
        Exception exception,
        string dependency,
        string correlationId,
        string messageId,
        double durationMs);

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Partner verification completed. Dependency={Dependency} PartnerId={PartnerId} StatusCode={StatusCode} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationCompleted(ILogger logger, string dependency, string partnerId, int statusCode, double durationMs);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Partner verification failed. Dependency={Dependency} PartnerId={PartnerId} ErrorType={ErrorType} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationFailed(ILogger logger, Exception exception, string dependency, string partnerId, string errorType, double durationMs);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Idempotency acquisition completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyAcquisitionCompleted(ILogger logger, string dependency, string outcome);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Idempotency cache lookup completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyCacheLookupCompleted(ILogger logger, string dependency, string outcome);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Idempotency cache storage completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyCacheStorageCompleted(ILogger logger, string dependency, string outcome);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Idempotency release completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyReleaseCompleted(ILogger logger, string dependency, string outcome);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "Consumer message observed. Dependency={Dependency} Consumer={Consumer} Destination={Destination} MessageId={MessageId} CorrelationId={CorrelationId} DeliveryCount={DeliveryCount}")]
    public static partial void ConsumerMessageObserved(
        ILogger logger,
        string dependency,
        string consumer,
        string destination,
        string messageId,
        string correlationId,
        int deliveryCount);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Warning,
        Message = "Consumer retry scheduled. Dependency={Dependency} Consumer={Consumer} Destination={Destination} DelaySeconds={DelaySeconds}")]
    public static partial void ConsumerRetryScheduled(
        ILogger logger,
        Exception exception,
        string dependency,
        string consumer,
        string destination,
        double delaySeconds);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Error,
        Message = "Consumer processing failed. Dependency={Dependency} Consumer={Consumer} Destination={Destination}")]
    public static partial void ConsumerProcessingFailed(
        ILogger logger,
        Exception exception,
        string dependency,
        string consumer,
        string destination);
}