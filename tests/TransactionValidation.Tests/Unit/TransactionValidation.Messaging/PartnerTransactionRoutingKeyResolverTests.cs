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
    /// <summary>
    /// Scenario: the partner transaction has been verified.
    /// Expected: the accepted routing key is returned.
    /// </summary>
    [Fact]
    public void Resolve_WhenPartnerIsVerified_ReturnsAcceptedRoutingKey()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver("partner.transaction");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: true));

        result.Should().Be("partner.transaction.accepted");
    }

    /// <summary>
    /// Scenario: the partner transaction has not been verified.
    /// Expected: the unverified routing key is returned.
    /// </summary>
    [Fact]
    public void Resolve_WhenPartnerIsNotVerified_ReturnsUnverifiedRoutingKey()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver("partner.transaction");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: false));

        result.Should().Be("partner.transaction.unverified");
    }

    /// <summary>
    /// Scenario: the routing-key prefix contains surrounding whitespace.
    /// Expected: the prefix is trimmed before the routing key is returned.
    /// </summary>
    [Fact]
    public void Resolve_WhenPrefixHasWhitespace_TrimsPrefix()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver(" custom.transaction ");

        var result = resolver.Resolve(CreateEnvelope(partnerVerified: true));

        result.Should().Be("custom.transaction.accepted");
    }

    /// <summary>
    /// Scenario: the transaction envelope is null.
    /// Expected: resolving the routing key throws an argument-null exception.
    /// </summary>
    [Fact]
    public void Resolve_WhenEnvelopeIsNull_ThrowsArgumentNullException()
    {
        var resolver = new PartnerTransactionRoutingKeyResolver("partner.transaction");

        var action = () => resolver.Resolve(null!);

        action.Should().Throw<ArgumentNullException>();
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
