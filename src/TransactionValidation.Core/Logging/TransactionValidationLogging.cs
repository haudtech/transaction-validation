using Microsoft.Extensions.Logging;

namespace TransactionValidation.Core.Logging;

/// <summary>
/// Source-generated structured logging events for transaction processing, partner verification, and idempotency.
/// </summary>
public static partial class TransactionValidationLogging
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Transaction request received. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference}")]
    public static partial void RequestReceived(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Transaction validation failed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} Reason={Reason}")]
    public static partial void ValidationFailed(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference,
        string reason);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Duplicate transaction response replayed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} MessageId={MessageId}")]
    public static partial void DuplicateReplayed(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference,
        string messageId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Idempotency key reused with a different payload. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference}")]
    public static partial void IdempotencyConflict(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Partner verification completed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationCompleted(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference,
        double durationMs);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Transaction published. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} MessageId={MessageId} DurationMs={DurationMs}")]
    public static partial void TransactionPublished(
        ILogger logger,
        string correlationId,
        string partnerId,
        string transactionReference,
        string messageId,
        double durationMs);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Error,
        Message = "Transaction processing failed. CorrelationId={CorrelationId} PartnerId={PartnerId} TransactionReference={TransactionReference} DurationMs={DurationMs}")]
    public static partial void ProcessingFailed(
        ILogger logger,
        Exception exception,
        string correlationId,
        string partnerId,
        string transactionReference,
        double durationMs);

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Partner verification completed. Dependency={Dependency} PartnerId={PartnerId} StatusCode={StatusCode} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationCompleted(
        ILogger logger,
        string dependency,
        string partnerId,
        int statusCode,
        double durationMs);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Partner verification failed. Dependency={Dependency} PartnerId={PartnerId} ErrorType={ErrorType} DurationMs={DurationMs}")]
    public static partial void PartnerVerificationFailed(
        ILogger logger,
        Exception exception,
        string dependency,
        string partnerId,
        string errorType,
        double durationMs);

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Idempotency acquisition completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyAcquisitionCompleted(
        ILogger logger,
        string dependency,
        string outcome);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Idempotency cache lookup completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyCacheLookupCompleted(
        ILogger logger,
        string dependency,
        string outcome);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Information,
        Message = "Idempotency cache storage completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyCacheStorageCompleted(
        ILogger logger,
        string dependency,
        string outcome);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Information,
        Message = "Idempotency release completed. Dependency={Dependency} Outcome={Outcome}")]
    public static partial void IdempotencyReleaseCompleted(
        ILogger logger,
        string dependency,
        string outcome);
}
