using FluentAssertions;

using TransactionValidation.Mock.Options;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Mock;

/// <summary>
/// Verifies Azure Service Bus consumer configuration metadata for the Azure consumer migration phase.
/// </summary>
public sealed class ServiceBusConsumerOptionsTests
{
    [Fact]
    public void ServiceBusPrimaryConsumerOptions_UsesExpectedSectionName()
    {
        ServiceBusPrimaryConsumerOptions.SectionName.Should().Be("ServiceBusConsumer");
    }

    [Fact]
    public void ServiceBusAuditConsumerOptions_UsesExpectedSectionName()
    {
        ServiceBusAuditConsumerOptions.SectionName.Should().Be("ServiceBusAuditConsumer");
    }
}
