using Azure.Messaging.ServiceBus;

using FluentAssertions;

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
    [Fact]
    public async Task PublishAsync_WhenCalled_SendsMessageWithExpectedMetadata()
    {
        var senderMock = new Mock<IServiceBusMessageSender>();
        var sut = new ServiceBusMessagePublisher(
            senderMock.Object,
            "partner.transactions",
            "partner.transaction",
            "partner.transaction.accepted",
            "partner.transaction.accepted");

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
