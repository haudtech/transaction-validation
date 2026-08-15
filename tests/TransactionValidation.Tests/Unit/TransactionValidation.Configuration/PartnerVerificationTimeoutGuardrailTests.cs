#nullable enable

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Integration;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class PartnerVerificationTimeoutGuardrailTests
{
    [Fact]
    public void HttpClientTimeout_WhenTotalTimeoutMissing_DerivesFromAttemptAndRetry()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["PartnerVerification:BaseUrl"] = "http://localhost:5002/",
            ["PartnerVerification:RetryCount"] = "2",
            ["PartnerVerification:TimeoutSeconds"] = "4",
            ["PartnerVerification:AttemptTimeoutSeconds"] = "4",
            ["PartnerVerification:TotalRequestTimeoutSeconds"] = "0"
        });

        var timeout = ReadPartnerVerifierHttpClientTimeout(provider);

        timeout.Should().Be(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void HttpClientTimeout_WhenTotalTimeoutIsNotGreaterThanAttempt_UsesAttemptPlusOneSecond()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["PartnerVerification:BaseUrl"] = "http://localhost:5002/",
            ["PartnerVerification:RetryCount"] = "3",
            ["PartnerVerification:TimeoutSeconds"] = "8",
            ["PartnerVerification:AttemptTimeoutSeconds"] = "8",
            ["PartnerVerification:TotalRequestTimeoutSeconds"] = "8"
        });

        var timeout = ReadPartnerVerifierHttpClientTimeout(provider);

        timeout.Should().Be(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void HttpClientTimeout_WhenTotalTimeoutProvided_UsesConfiguredValue()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["PartnerVerification:BaseUrl"] = "http://localhost:5002/",
            ["PartnerVerification:RetryCount"] = "1",
            ["PartnerVerification:TimeoutSeconds"] = "5",
            ["PartnerVerification:AttemptTimeoutSeconds"] = "5",
            ["PartnerVerification:TotalRequestTimeoutSeconds"] = "20"
        });

        var timeout = ReadPartnerVerifierHttpClientTimeout(provider);

        timeout.Should().Be(TimeSpan.FromSeconds(20));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        services.AddTransactionValidationCommonServices(configuration);

        return services.BuildServiceProvider();
    }

    private static TimeSpan ReadPartnerVerifierHttpClientTimeout(ServiceProvider provider)
    {
        var verifier = provider.GetRequiredService<IPartnerVerifier>();
        verifier.Should().BeOfType<PartnerVerifierClient>();

        var field = typeof(PartnerVerifierClient)
            .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        var httpClient = field!.GetValue(verifier).Should().BeOfType<HttpClient>().Subject;
        return httpClient.Timeout;
    }
}
