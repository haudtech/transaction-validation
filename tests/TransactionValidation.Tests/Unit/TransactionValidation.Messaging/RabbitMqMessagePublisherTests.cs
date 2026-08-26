using FluentAssertions;
using Moq;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Models;
using TransactionValidation.Messaging;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Messaging;

/// <summary>
/// Verifies RabbitMQ publisher behavior, compatibility invocation, and confirm handling.
/// </summary>
public sealed class RabbitMqMessagePublisherTests
{
    [Fact]
    public async Task PublishAsync_WhenPublisherConfirms_PublishesToExchangeWithRoutingAndHeaders()
    {
        var adapterMock = new Mock<IRabbitMqClientAdapter>();
        var resolverMock = new Mock<IMessageRoutingKeyResolver>();
        resolverMock
            .Setup(x => x.Resolve(It.IsAny<TransactionEnvelope>()))
            .Returns("partner.transaction.accepted");
        adapterMock
            .Setup(x => x.PublishPersistentWithConfirmAsync(
                "partner.transactions",
                "partner.transaction.accepted",
                It.IsAny<string>(),
                It.Is<IReadOnlyDictionary<string, object>>(headers =>
                    (string)headers["message-type"] == "PartnerTransactionAccepted"
                    && (string)headers["message-version"] == "1"
                    && headers.ContainsKey("correlation-id")
                    && headers.ContainsKey("message-id")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new RabbitMqMessagePublisher("partner.transactions", adapterMock.Object, resolverMock.Object);

        await sut.PublishAsync(CreateEnvelope(), CancellationToken.None);

        adapterMock.Verify(x => x.DeclareDurableQueueAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        adapterMock.Verify(x => x.PublishPersistentWithConfirmAsync(
            "partner.transactions",
            "partner.transaction.accepted",
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenPublisherConfirmFails_ThrowsConflictException()
    {
        var adapterMock = new Mock<IRabbitMqClientAdapter>();
        var resolverMock = new Mock<IMessageRoutingKeyResolver>();
        resolverMock
            .Setup(x => x.Resolve(It.IsAny<TransactionEnvelope>()))
            .Returns("partner.transaction.accepted");
        adapterMock
            .Setup(x => x.PublishPersistentWithConfirmAsync(
                "partner.transactions",
                "partner.transaction.accepted",
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new RabbitMqMessagePublisher("partner.transactions", adapterMock.Object, resolverMock.Object);

        var action = async () => await sut.PublishAsync(CreateEnvelope(), CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
    }

    private static TransactionEnvelope CreateEnvelope()
    {
        return new TransactionEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            ReceivedAt = DateTimeOffset.UtcNow,
            PartnerVerified = true,
            Transaction = new PartnerTransactionRequest
            {
                PartnerId = "partner-123",
                TransactionReference = "ref-001",
                Amount = 100m,
                Currency = "USD",
                Timestamp = DateTime.UtcNow
            }
        };
    }

}
