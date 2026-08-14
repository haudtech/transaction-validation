using FluentAssertions;
using Moq;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Models;
using TransactionValidation.Messaging;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Messaging;

public sealed class RabbitMqMessagePublisherTests
{
    [Fact]
    public async Task PublishAsync_WhenPublisherConfirms_DeclaresQueueAndPublishes()
    {
        var adapterMock = new Mock<IRabbitMqClientAdapter>();
        adapterMock
            .Setup(x => x.PublishPersistentWithConfirmAsync("partner-transactions", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new RabbitMqMessagePublisher("partner-transactions", true, adapterMock.Object);

        await sut.PublishAsync(CreateEnvelope(), CancellationToken.None);

        adapterMock.Verify(x => x.DeclareDurableQueueAsync("partner-transactions", true, It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(x => x.PublishPersistentWithConfirmAsync("partner-transactions", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenPublisherConfirmFails_ThrowsConflictException()
    {
        var adapterMock = new Mock<IRabbitMqClientAdapter>();
        adapterMock
            .Setup(x => x.PublishPersistentWithConfirmAsync("partner-transactions", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new RabbitMqMessagePublisher("partner-transactions", true, adapterMock.Object);

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
