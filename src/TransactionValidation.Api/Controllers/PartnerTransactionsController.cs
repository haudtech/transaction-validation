using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Api.Idempotency;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Api.Controllers;

/// <summary>
/// Accepts partner transaction submissions, validates them, enforces idempotency, verifies the partner, and publishes an accepted envelope to RabbitMQ.
/// This controller implements the request flow described in docs/analysis/solution_analysis.md and the system context in docs/architecture_design/Architecture_design.md.
/// </summary>
[ApiController]
[Route("api/v1/partner/transactions")]
public sealed class PartnerTransactionsController : ControllerBase
{
    private readonly IValidator<PartnerTransactionRequest> _validator;
    private readonly IPartnerVerifier _partnerVerifier;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IIdempotencyStore _idempotencyStore;

    public PartnerTransactionsController(
        IValidator<PartnerTransactionRequest> validator,
        IPartnerVerifier partnerVerifier,
        IMessagePublisher messagePublisher,
        IIdempotencyStore idempotencyStore)
    {
        _validator = validator;
        _partnerVerifier = partnerVerifier;
        _messagePublisher = messagePublisher;
        _idempotencyStore = idempotencyStore;
    }

    /// <summary>
    /// Validates a partner transaction request, enforces idempotency, verifies the partner, and publishes the accepted envelope to RabbitMQ.
    /// </summary>
    /// <param name="request">Inbound partner transaction payload from the client.</param>
    /// <param name="cancellationToken">Token used to stop processing during shutdown or client cancellation.</param>
    /// <returns>An accepted response carrying the broker message identifier and correlation identifier.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] PartnerTransactionRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new BadRequestException("request body is required.");
        }

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessage = string.Join(
                "; ",
                validationResult.Errors
                    .Select(error => error.ErrorMessage)
                    .Distinct(StringComparer.Ordinal));

            throw new BadRequestException(errorMessage);
        }

        var idempotencyKey = BuildIdempotencyKey(request);
        var requestFingerprint = BuildRequestFingerprint(request);
        var acquireResult = _idempotencyStore.TryAcquire(idempotencyKey, requestFingerprint, DateTimeOffset.UtcNow);

        if (acquireResult == IdempotencyAcquireResult.Duplicate)
        {
            if (_idempotencyStore.TryGetCachedResponse(idempotencyKey, requestFingerprint, DateTimeOffset.UtcNow, out var cachedResponse))
            {
                return Accepted(new
                {
                    messageId = cachedResponse.MessageId,
                    correlationId = cachedResponse.CorrelationId,
                    status = cachedResponse.Status.ToString().ToLowerInvariant()
                });
            }

            throw new ConflictException("Duplicate transaction detected within the idempotency window.");
        }

        if (acquireResult == IdempotencyAcquireResult.KeyReusedWithDifferentPayload)
        {
            throw new ConflictException("Idempotency key was already used with a different request payload.");
        }

        try
        {
            var partnerVerified = await _partnerVerifier.VerifyAsync(request.PartnerId, cancellationToken);

            var correlationId = string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                ? Guid.NewGuid().ToString("N")
                : HttpContext.TraceIdentifier;

            var envelope = new TransactionEnvelope
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                ReceivedAt = DateTimeOffset.UtcNow,
                Transaction = request,
                PartnerVerified = partnerVerified
            };

            await _messagePublisher.PublishAsync(envelope, cancellationToken);

            var acceptedResponse = new IdempotencyCachedResponse(
                envelope.MessageId,
                envelope.CorrelationId,
                IdempotencyCachedResponseStatus.Accepted);

            _idempotencyStore.StoreCachedResponse(idempotencyKey, requestFingerprint, DateTimeOffset.UtcNow, acceptedResponse);

            return Accepted(new
            {
                messageId = acceptedResponse.MessageId,
                correlationId = acceptedResponse.CorrelationId,
                status = acceptedResponse.Status.ToString().ToLowerInvariant()
            });
        }
        catch
        {
            _idempotencyStore.Release(idempotencyKey);
            throw;
        }
    }

    /// <summary>
    /// Builds the idempotency key from the partner identifier and either a caller-supplied idempotency header or the transaction reference.
    /// </summary>
    /// <param name="request">The request being evaluated for duplicate suppression.</param>
    /// <returns>A stable key that identifies the same logical transaction across retries.</returns>
    private string BuildIdempotencyKey(PartnerTransactionRequest request)
    {
        var partnerId = request.PartnerId.Trim();

        if (Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyHeader)
            && !string.IsNullOrWhiteSpace(idempotencyHeader))
        {
            return $"{partnerId}|{idempotencyHeader.ToString().Trim()}";
        }

        return $"{partnerId}|{request.TransactionReference.Trim()}";
    }

    /// <summary>
    /// Creates a canonical hash of the request body so the same payload can be recognized across retries while different payloads with the same idempotency key are rejected.
    /// </summary>
    /// <param name="request">The transaction payload to fingerprint.</param>
    /// <returns>A stable SHA-256 fingerprint used for duplicate detection.</returns>
    private static string BuildRequestFingerprint(PartnerTransactionRequest request)
    {
        var canonical = string.Join(
            "|",
            request.PartnerId.Trim(),
            request.TransactionReference.Trim(),
            request.Amount.ToString("0.############################", CultureInfo.InvariantCulture),
            request.Currency.Trim().ToUpperInvariant(),
            request.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
