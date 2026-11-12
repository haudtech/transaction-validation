using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Secrets.Options;

namespace TransactionValidation.Gateway.Secrets.Validators;

public sealed class SecretProviderOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<SecretProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, SecretProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail("Secrets:Provider must explicitly select a registered secret provider.");
        }

        if (!string.Equals(options.Provider, SecretProviderOptions.LocalFileProvider, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"Secret provider '{options.Provider}' is not registered. Configure an implemented provider explicitly.");
        }

        if (!environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                "The LocalFile secret provider is supported only in the Development environment.");
        }

        if (string.IsNullOrWhiteSpace(options.Directory))
        {
            return ValidateOptionsResult.Fail("Secrets:Directory is required for the LocalFile provider.");
        }

        return ValidateOptionsResult.Success;
    }
}
