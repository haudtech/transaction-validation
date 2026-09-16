using Azure.Messaging.ServiceBus;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using TransactionValidation.Core.Models;
using TransactionValidation.Messaging;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Messaging;

/// <summary>
/// Verifies Azure Service Bus publisher behavior and message metadata generation.
/// </summary>
public sealed class ServiceBusMessagePublisherTests
{
    /// <summary>
    /// Scenario: a transaction envelope is published successfully.
    /// Expected: one Service Bus message is sent with the required routing and correlation metadata.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenCalled_SendsMessageWithExpectedMetadata()
    {
        var senderMock = new Mock<IServiceBusMessageSender>();
        var sut = new ServiceBusMessagePublisher(
            senderMock.Object,
            "partner.transactions",
            "partner.transaction",
            "partner.transaction.accepted",
            "partner.transaction.accepted",
            NullLogger<ServiceBusMessagePublisher>.Instance);

        await sut.PublishAsync(CreateEnvelope(), CancellationToken.None);

        senderMock.Verify(x => x.SendMessageAsync(
            It.Is<ServiceBusMessage>(message =>
                message.Subject == "partner.transaction"
                && message.ApplicationProperties["routingKey"].ToString() == "partner.transaction.accepted"
                && message.ApplicationProperties["eventType"].ToString() == "partner.transaction.accepted"
                && message.ApplicationProperties["message-type"].ToString() == "PartnerTransactionAccepted"
                && message.ApplicationProperties["message-version"].ToString() == "1"
                && message.ApplicationProperties["correlation-id"].ToString() == "corr-123"
                && message.ApplicationProperties["message-id"].ToString() == "msg-123"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Scenario: the Service Bus sender throws during publication.
    /// Expected: the original exception is logged by the publisher and rethrown to the caller.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenSenderFails_RethrowsTheException()
    {
        var senderMock = new Mock<IServiceBusMessageSender>();
        var expected = new InvalidOperationException("send failed");
        senderMock
            .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var sut = new ServiceBusMessagePublisher(
            senderMock.Object,
            "partner.transactions",
            "partner.transaction",
            "partner.transaction.accepted",
            "partner.transaction.accepted",
            NullLogger<ServiceBusMessagePublisher>.Instance);

        var action = () => sut.PublishAsync(CreateEnvelope(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("send failed");
    }

    private static TransactionEnvelope CreateEnvelope()
    {
        return new TransactionEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-123",
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
