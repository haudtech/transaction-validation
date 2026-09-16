using FluentAssertions;

using TransactionValidation.Configuration.Options;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class ServiceBusPublisherOptionsValidatorTests
{
    /// <summary>
    /// Scenario: all Service Bus publisher settings are present.
    /// Expected: validation returns the original options instance.
    /// </summary>
    [Fact]
    public void Validate_WhenAllRequiredValuesArePresent_ReturnsOptions()
    {
        var options = CreateValidOptions();

        var result = ServiceBusPublisherOptionsValidator.Validate(options);

        result.Should().BeSameAs(options);
    }

    /// <summary>
    /// Scenario: both Service Bus connection mechanisms are missing.
    /// Expected: validation reports the connection requirement.
    /// </summary>
    [Fact]
    public void Validate_WhenConnectionDetailsAreMissing_ReportsConnectionRequirement()
    {
        var options = CreateValidOptions();
        options.ConnectionString = string.Empty;
        options.Namespace = string.Empty;

        var action = () => ServiceBusPublisherOptionsValidator.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString or Namespace*");
    }

    /// <summary>
    /// Scenario: Service Bus topic metadata is missing.
    /// Expected: validation reports every required metadata property.
    /// </summary>
    [Fact]
    public void Validate_WhenTopicMetadataIsMissing_ReportsAllMissingProperties()
    {
        var action = () => ServiceBusPublisherOptionsValidator.Validate(new ServiceBusPublisherOptions());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*TopicName*")
            .WithMessage("*Subject*")
            .WithMessage("*RoutingKey*")
            .WithMessage("*EventType*");
    }

    /// <summary>
    /// Scenario: the publisher options argument is null.
    /// Expected: validation throws an argument-null exception.
    /// </summary>
    [Fact]
    public void Validate_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var action = () => ServiceBusPublisherOptionsValidator.Validate(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    private static ServiceBusPublisherOptions CreateValidOptions()
    {
        return new ServiceBusPublisherOptions
        {
            ConnectionString = "Endpoint=sb://integration.test/;",
            TopicName = "partner.transactions",
            Subject = "partner.transaction.accepted",
            RoutingKey = "partner.transaction.accepted",
            EventType = "PartnerTransactionAccepted"
        };
    }
}
