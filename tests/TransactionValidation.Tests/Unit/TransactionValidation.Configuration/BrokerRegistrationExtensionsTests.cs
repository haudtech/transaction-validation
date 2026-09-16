using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Configuration.Options;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class BrokerRegistrationExtensionsTests
{
    /// <summary>
    /// Scenario: no broker type is configured.
    /// Expected: RabbitMQ registration is selected.
    /// </summary>
    [Fact]
    public void AddConfiguredBroker_WhenBrokerIsNotConfigured_UsesRabbitMqRegistration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();
        var rabbitRegistered = false;
        var azureRegistered = false;

        services.AddConfiguredBroker(
            configuration,
            (_, _) => rabbitRegistered = true,
            (_, _) => azureRegistered = true);

        rabbitRegistered.Should().BeTrue();
        azureRegistered.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Azure Service Bus is selected as the broker.
    /// Expected: Azure Service Bus registration is selected.
    /// </summary>
    [Fact]
    public void AddConfiguredBroker_WhenAzureServiceBusIsSelected_UsesAzureRegistration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(($"{BrokerTypeOptions.SectionName}:BrokerType", BrokerTypeOptions.AzureServiceBus));
        var rabbitRegistered = false;
        var azureRegistered = false;

        services.AddConfiguredBroker(
            configuration,
            (_, _) => rabbitRegistered = true,
            (_, _) => azureRegistered = true);

        rabbitRegistered.Should().BeFalse();
        azureRegistered.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Azure Service Bus is selected without a registration delegate.
    /// Expected: an invalid-operation exception explains the missing registration.
    /// </summary>
    [Fact]
    public void AddConfiguredBroker_WhenAzureRegistrationIsMissing_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(($"{BrokerTypeOptions.SectionName}:BrokerType", BrokerTypeOptions.AzureServiceBus));

        var action = () => services.AddConfiguredBroker(configuration, (_, _) => { });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Azure Service Bus broker registration is not configured*");
    }

    /// <summary>
    /// Scenario: an unsupported broker type is configured.
    /// Expected: an invalid-operation exception identifies the unsupported value.
    /// </summary>
    [Fact]
    public void AddConfiguredBroker_WhenBrokerIsUnsupported_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(($"{BrokerTypeOptions.SectionName}:BrokerType", "Unsupported"));

        var action = () => services.AddConfiguredBroker(configuration, (_, _) => { });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported broker configuration: Unsupported*");
    }

    /// <summary>
    /// Scenario: a broker type is configured in the configuration source.
    /// Expected: the bound broker-selection options are returned.
    /// </summary>
    [Fact]
    public void GetBrokerSelection_WhenBrokerIsConfigured_ReturnsBoundOptions()
    {
        var configuration = BuildConfiguration(($"{BrokerTypeOptions.SectionName}:BrokerType", BrokerTypeOptions.AzureServiceBus));

        var result = configuration.GetBrokerSelection();

        result.BrokerType.Should().Be(BrokerTypeOptions.AzureServiceBus);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        var settings = values.ToDictionary(value => value.Key, value => (string?)value.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
