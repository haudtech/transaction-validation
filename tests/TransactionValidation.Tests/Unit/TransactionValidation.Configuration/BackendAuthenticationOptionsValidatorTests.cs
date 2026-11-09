using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using TransactionValidation.Configuration.Options;

using Xunit;

namespace TransactionValidation.Configuration.Tests;

public sealed class BackendAuthenticationOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenDirectApiKeyModeIsNotDevelopment_Fails()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:ApiKey"] = "test-key" })
            .Build();
        var validator = new BackendAuthenticationOptionsValidator(configuration, CreateEnvironment("Production"));

        var result = validator.Validate(
            null,
            new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.DirectApiKey });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDirectApiKeyModeIsDevelopmentWithConfiguredKey_Succeeds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:ApiKey"] = "test-key" })
            .Build();
        var validator = new BackendAuthenticationOptionsValidator(configuration, CreateEnvironment("Development"));

        var result = validator.Validate(
            null,
            new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.DirectApiKey });

        result.Succeeded.Should().BeTrue();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestHostEnvironment(environmentName);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "TransactionValidation.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
