using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Secrets.Extensions;
using TransactionValidation.Gateway.Secrets.Options;
using TransactionValidation.Gateway.Secrets.Validators;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class SecretProviderOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenLocalFileSelectedInDevelopment_Succeeds()
    {
        var validator = new SecretProviderOptionsValidator(new TestHostEnvironment("Development"));

        var result = validator.Validate(
            Options.DefaultName,
            new SecretProviderOptions { Provider = SecretProviderOptions.LocalFileProvider });

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    [Fact]
    public void Validate_WhenLocalFileSelectedOutsideDevelopment_Fails()
    {
        var validator = new SecretProviderOptionsValidator(new TestHostEnvironment("Production"));

        var result = validator.Validate(
            Options.DefaultName,
            new SecretProviderOptions { Provider = SecretProviderOptions.LocalFileProvider });

        Assert.False(result.Succeeded);
        Assert.Contains("only in the Development environment", result.FailureMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UnknownProvider")]
    public void Validate_WhenProviderIsMissingOrUnsupported_Fails(string provider)
    {
        var validator = new SecretProviderOptionsValidator(new TestHostEnvironment("Development"));

        var result = validator.Validate(
            Options.DefaultName,
            new SecretProviderOptions { Provider = provider });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenLocalFileDirectoryIsMissing_Fails()
    {
        var validator = new SecretProviderOptionsValidator(new TestHostEnvironment("Development"));

        var result = validator.Validate(
            Options.DefaultName,
            new SecretProviderOptions
            {
                Provider = SecretProviderOptions.LocalFileProvider,
                Directory = " "
            });

        Assert.False(result.Succeeded);
        Assert.Contains("Secrets:Directory is required", result.FailureMessage);
    }

    [Fact]
    public void AddConfiguredSecretProvider_WhenWriterIsRequired_RegistersWriterForSelectedProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Provider"] = SecretProviderOptions.LocalFileProvider,
                ["Secrets:Directory"] = ".secrets"
            })
            .Build();
        var environment = new TestHostEnvironment("Development");
        var services = new ServiceCollection();

        services.AddSingleton<IHostEnvironment>(environment);
        services.AddConfiguredSecretProvider(configuration, environment, requireWriter: true);

        using var provider = services.BuildServiceProvider();
        var secretProvider = provider.GetRequiredService<ISecretProvider>();
        var secretWriter = provider.GetRequiredService<ISecretWriter>();

        Assert.Same(secretProvider, secretWriter);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "TransactionValidation.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
