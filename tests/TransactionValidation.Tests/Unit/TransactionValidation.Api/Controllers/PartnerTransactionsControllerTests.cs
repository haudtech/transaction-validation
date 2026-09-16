using System.Reflection;

using FluentAssertions;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using TransactionValidation.Api.Controllers;
using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Controllers;

/// <summary>
/// Verifies controller-level validation, idempotency, verification, and publish orchestration paths.
/// </summary>
public sealed class PartnerTransactionsControllerTests
{
    /// <summary>
    /// Scenario: the request fails validation.
    /// Expected: controller execution throws a bad-request exception.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRequestIsInvalid_ThrowsBadRequestException()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>(MockBehavior.Strict);
        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var request = CreateValidRequest();
        request = new PartnerTransactionRequest
        {
            PartnerId = request.PartnerId,
            TransactionReference = request.TransactionReference,
            Amount = request.Amount,
            Currency = "ZZZ",
            Timestamp = request.Timestamp
        };

        var action = async () => await sut.CreateAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*currency must be a valid ISO-4217 code.*");
    }

    /// <summary>
    /// Scenario: the request body is null.
    /// Expected: controller execution throws a bad-request exception.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ThrowsBadRequestException()
    {
        var sut = CreateSut(
            new PartnerTransactionRequestValidator(),
            Moq.Mock.Of<IPartnerVerifier>(),
            Moq.Mock.Of<IMessagePublisher>(),
            Moq.Mock.Of<IIdempotencyStore>());

        var action = async () => await sut.CreateAsync(null!, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("request body is required.");
    }

    /// <summary>
    /// Scenario: validation fails because the request has missing partner and transaction metadata.
    /// Expected: the controller still builds an error context using empty strings for null values.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenValidationFailsWithNullPartnerAndReference_UsesEmptyStringsInContext()
    {
        var sut = CreateSut(new PartnerTransactionRequestValidator(), Moq.Mock.Of<IPartnerVerifier>(), Moq.Mock.Of<IMessagePublisher>(), Moq.Mock.Of<IIdempotencyStore>());
        var request = new PartnerTransactionRequest
        {
            PartnerId = null,
            TransactionReference = null,
            Amount = 99m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };

        var action = async () => await sut.CreateAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    /// <summary>
    /// Scenario: partner verification fails with not found.
    /// Expected: the exception propagates, publication is skipped, and the idempotency claim is released.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenPartnerVerificationFails_PropagatesNotFoundException()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ThrowsAsync(new NotFoundException("Partner 'partner-123' could not be verified."));

        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release("partner-123|ref-001"), Times.Once);
    }

    /// <summary>
    /// Scenario: message publication fails with a conflict.
    /// Expected: the exception propagates and the idempotency claim is released.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenPublishFails_PropagatesConflictException()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("RabbitMQ did not confirm message publishing."));

        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        idempotencyStore.Verify(x => x.Release("partner-123|ref-001"), Times.Once);
    }

    /// <summary>
    /// Scenario: a duplicate request has a cached accepted response.
    /// Expected: the cached HTTP 202 response is returned without reprocessing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenDuplicateRequestWithCachedResponse_ReturnsAcceptedWithoutProcessing()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>();
        var cachedResponse = new IdempotencyCachedResponse("msg-123", "corr-123", IdempotencyCachedResponseStatus.Accepted);

        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Duplicate);
        idempotencyStore
            .Setup(x => x.TryGetCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), out cachedResponse))
            .Returns(true);

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var result = await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);

        accepted.Value.Should().BeEquivalentTo(new
        {
            messageId = "msg-123",
            correlationId = "corr-123",
            status = "accepted"
        });

        partnerVerifier.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool?>()), Times.Never);
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Scenario: a duplicate request has no cached response.
    /// Expected: a conflict exception is thrown without reprocessing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenDuplicateRequestWithoutCachedResponse_ThrowsConflictExceptionWithoutProcessing()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>();
        var ignoredResponse = new IdempotencyCachedResponse("msg-ignored", "corr-ignored", IdempotencyCachedResponseStatus.Accepted);

        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Duplicate);
        idempotencyStore
            .Setup(x => x.TryGetCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), out ignoredResponse))
            .Returns(false);

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Duplicate transaction detected*");

        partnerVerifier.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool?>()), Times.Never);
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Scenario: an idempotency key is reused with a different payload.
    /// Expected: a conflict exception is thrown without invoking downstream services.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenIdempotencyKeyIsReusedWithDifferentPayload_ThrowsConflictException()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|idemp-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.KeyReusedWithDifferentPayload);

        var sut = CreateSut(
            new PartnerTransactionRequestValidator(),
            partnerVerifier.Object,
            publisher.Object,
            idempotencyStore.Object,
            "idemp-001");

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage("*different request payload*");

        partnerVerifier.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool?>()), Times.Never);
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Scenario: a valid request passes verification and publication.
    /// Expected: an accepted result is returned and the envelope is cached.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ReturnsAcceptedAndPublishesEnvelope()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);

        TransactionEnvelope? capturedEnvelope = null;
        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionEnvelope, CancellationToken>((envelope, _) => capturedEnvelope = envelope)
            .Returns(Task.CompletedTask);

        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var result = await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);

        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.PartnerVerified.Should().BeTrue();
        capturedEnvelope.Transaction.PartnerId.Should().Be("partner-123");
        capturedEnvelope.CorrelationId.Should().Be("trace-123");

        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
        idempotencyStore.Verify(x => x.TryAcquire(
            "partner-123|ref-001",
            It.Is<string>(fingerprint => fingerprint.Length == 64),
            It.IsAny<DateTimeOffset>()), Times.Once);
        idempotencyStore.Verify(x => x.StoreCachedResponse(
            "partner-123|ref-001",
            It.Is<string>(fingerprint => fingerprint.Length == 64),
            It.IsAny<DateTimeOffset>(),
            It.Is<IdempotencyCachedResponse>(cached =>
                cached.Status == IdempotencyCachedResponseStatus.Accepted
                && !string.IsNullOrWhiteSpace(cached.MessageId)
                && !string.IsNullOrWhiteSpace(cached.CorrelationId))), Times.Once);
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Scenario: a valid request supplies an idempotency header.
    /// Expected: the header-derived key is used for acquisition and caching.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenIdempotencyHeaderIsProvided_UsesHeaderBasedKey()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|idemp-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|idemp-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(
            new PartnerTransactionRequestValidator(),
            partnerVerifier.Object,
            publisher.Object,
            idempotencyStore.Object,
            "idemp-001");

        var result = await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        idempotencyStore.Verify(x => x.TryAcquire(
            "partner-123|idemp-001",
            It.Is<string>(fingerprint => fingerprint.Length == 64),
            It.IsAny<DateTimeOffset>()), Times.Once);
        idempotencyStore.Verify(x => x.StoreCachedResponse(
            "partner-123|idemp-001",
            It.Is<string>(fingerprint => fingerprint.Length == 64),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<IdempotencyCachedResponse>()), Times.Once);
    }

    /// <summary>
    /// Scenario: no correlation context item exists and the trace identifier is blank.
    /// Expected: the controller generates a GUID correlation identifier in the accepted response.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenTraceIdentifierIsBlank_GeneratesCorrelationId()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);

        TransactionEnvelope? capturedEnvelope = null;
        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionEnvelope, CancellationToken>((envelope, _) => capturedEnvelope = envelope)
            .Returns(Task.CompletedTask);

        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object, traceIdentifier: string.Empty);

        var result = await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        var payload = accepted.Value.Should().BeAssignableTo<object>().Subject;
        var correlationIdProperty = payload.GetType().GetProperty("correlationId");
        correlationIdProperty.Should().NotBeNull();
        var correlationId = correlationIdProperty!.GetValue(payload)?.ToString();

        correlationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParseExact(correlationId, "N", out _).Should().BeTrue();
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.CorrelationId.Should().Be(correlationId);
    }

    /// <summary>
    /// Scenario: the correlation-context item is already set on the HTTP context.
    /// Expected: the controller prefers the correlation context value over the trace identifier.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenCorrelationContextExists_UsesCorrelationContextValue()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>();
        partnerVerifier
            .Setup(x => x.VerifyAsync("partner-123", It.IsAny<CancellationToken>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Acquired);
        idempotencyStore
            .Setup(x => x.StoreCachedResponse("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IdempotencyCachedResponse>()));

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);
        sut.ControllerContext.HttpContext.Items[CorrelationContextMiddleware.CorrelationIdItemKey] = "ctx-correlation-456";

        var result = await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        var accepted = (AcceptedResult)result;
        var payload = accepted.Value!
            .GetType()
            .GetProperty("correlationId")!
            .GetValue(accepted.Value)!
            .ToString();
        payload.Should().Be("ctx-correlation-456");
    }

    /// <summary>
    /// Scenario: the idempotency key builder sees a blank idempotency header.
    /// Expected: the fallback request reference is used and blank request values collapse to empty strings.
    /// </summary>
    [Fact]
    public void BuildIdempotencyKey_WhenHeaderIsBlankOrRequestFieldsAreNull_UsesFallbackAndEmptyStrings()
    {
        var sut = CreateSut(new PartnerTransactionRequestValidator(), new Mock<IPartnerVerifier>().Object, new Mock<IMessagePublisher>().Object, new Mock<IIdempotencyStore>().Object);
        var method = typeof(PartnerTransactionsController).GetMethod("BuildIdempotencyKey", BindingFlags.Instance | BindingFlags.NonPublic);

        var blankHeaderRequest = new PartnerTransactionRequest
        {
            PartnerId = "partner-123",
            TransactionReference = "ref-001",
            Amount = 10m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
        sut.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] = " ";

        var resultFromBlankHeader = (string)method!.Invoke(sut, [blankHeaderRequest])!;
        resultFromBlankHeader.Should().Be("partner-123|ref-001");

        var nullRequest = new PartnerTransactionRequest
        {
            PartnerId = null!,
            TransactionReference = null!,
            Amount = 10m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
        sut.ControllerContext.HttpContext.Request.Headers.Remove("Idempotency-Key");

        var resultFromNulls = (string)method.Invoke(sut, [nullRequest])!;
        resultFromNulls.Should().Be("|");
    }

    private static PartnerTransactionsController CreateSut(
        IValidator<PartnerTransactionRequest> validator,
        IPartnerVerifier partnerVerifier,
        IMessagePublisher publisher,
        IIdempotencyStore idempotencyStore,
        string? idempotencyKeyHeader = null,
        string? traceIdentifier = "trace-123")
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier ?? "trace-123"
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKeyHeader;
        }

        var controller = new PartnerTransactionsController(validator, partnerVerifier, publisher, idempotencyStore, NullLogger<PartnerTransactionsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    private static PartnerTransactionRequest CreateValidRequest()
    {
        return new PartnerTransactionRequest
        {
            PartnerId = "partner-123",
            TransactionReference = "ref-001",
            Amount = 100m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
    }
}
