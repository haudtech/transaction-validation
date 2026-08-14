#nullable enable

using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TransactionValidation.Api.Controllers;
using TransactionValidation.Api.Idempotency;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Controllers;

public sealed class PartnerTransactionsControllerTests
{
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
            .WithMessage("*currency must be a supported ISO code.*");
    }

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

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release("partner-123|ref-001"), Times.Once);
    }

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

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        idempotencyStore.Verify(x => x.Release("partner-123|ref-001"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateRequest_ThrowsConflictExceptionWithoutProcessing()
    {
        var partnerVerifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var idempotencyStore = new Mock<IIdempotencyStore>();
        idempotencyStore
            .Setup(x => x.TryAcquire("partner-123|ref-001", It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(IdempotencyAcquireResult.Duplicate);

        var sut = CreateSut(new PartnerTransactionRequestValidator(), partnerVerifier.Object, publisher.Object, idempotencyStore.Object);

        var action = async () => await sut.CreateAsync(CreateValidRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Duplicate transaction detected*");

        partnerVerifier.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool?>()), Times.Never);
        publisher.Verify(x => x.PublishAsync(It.IsAny<TransactionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

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
        idempotencyStore.Verify(x => x.Release(It.IsAny<string>()), Times.Never);
    }

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
    }

    private static PartnerTransactionsController CreateSut(
        IValidator<PartnerTransactionRequest> validator,
        IPartnerVerifier partnerVerifier,
        IMessagePublisher publisher,
        IIdempotencyStore idempotencyStore,
        string? idempotencyKeyHeader = null)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123"
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKeyHeader;
        }

        var controller = new PartnerTransactionsController(validator, partnerVerifier, publisher, idempotencyStore)
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