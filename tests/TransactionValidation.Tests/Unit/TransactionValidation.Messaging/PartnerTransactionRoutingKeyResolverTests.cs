using FluentAssertions;
using TransactionValidation.Core.Models;
using TransactionValidation.Messaging;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Messaging;

/// <summary>
/// Verifies partner transaction routing-key resolution.
/// </summary>
public sealed class PartnerTransactionRoutingKeyResolverTests
{
    [Fact]
    public void Resolve_WhenPartnerIsVerified_ReturnsAcceptedRoutingKey()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver("partner.transaction");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: true));

        result.Should().Be("partner.transaction.accepted");
    }

    [Fact]
    public void Resolve_WhenPartnerIsNotVerified_ReturnsUnverifiedRoutingKey()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver("partner.transaction");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: false));

        result.Should().Be("partner.transaction.unverified");
    }

    [Fact]
    public void Resolve_WhenPrefixHasWhitespace_TrimsPrefix()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver(" custom.transaction ");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: true));

        result.Should().Be("custom.transaction.accepted");
    }

    private static TransactionEnvelope CreateEnvelope(bool partnerVerified)
    {
        return new TransactionEnvelope
        {
            MessageId = "message-001",
            CorrelationId = "correlation-001",
            ReceivedAt = DateTimeOffset.UtcNow,
            PartnerVerified = partnerVerified,
            Transaction = new PartnerTransactionRequest
            {
                PartnerId = "partner-123",
                TransactionReference = "reference-001",
                Amount = 100m,
                Currency = "USD",
                Timestamp = DateTime.UtcNow
            }
        };
    }
}
