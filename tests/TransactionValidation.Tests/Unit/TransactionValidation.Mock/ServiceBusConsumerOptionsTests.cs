using FluentAssertions;

using TransactionValidation.Mock.Options;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Mock;

/// <summary>
/// Verifies Azure Service Bus consumer configuration metadata for the Azure consumer migration phase.
/// </summary>
public sealed class ServiceBusConsumerOptionsTests
{
    /// <summary>
    /// Scenario: the primary Service Bus consumer options are inspected.
    /// Expected: the primary consumer section name is returned.
    /// </summary>
    [Fact]
    public void ServiceBusPrimaryConsumerOptions_UsesExpectedSectionName()
    {
        ServiceBusPrimaryConsumerOptions.SectionName.Should().Be("ServiceBusConsumer");
    }

    /// <summary>
    /// Scenario: the audit Service Bus consumer options are inspected.
    /// Expected: the audit consumer section name is returned.
    /// </summary>
    [Fact]
    public void ServiceBusAuditConsumerOptions_UsesExpectedSectionName()
    {
        ServiceBusAuditConsumerOptions.SectionName.Should().Be("ServiceBusAuditConsumer");
    }
}
