using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TransactionValidation.Configuration.Options;

public sealed class BackendAuthenticationOptionsValidator : IValidateOptions<BackendAuthenticationOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public BackendAuthenticationOptionsValidator(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, BackendAuthenticationOptions options)
    {
        return options.AuthenticationMode switch
        {
            BackendAuthenticationMode.Gateway => string.IsNullOrWhiteSpace(options.GatewayKey)
                ? ValidateOptionsResult.Fail("Gateway authentication mode requires a gateway credential.")
                : ValidateOptionsResult.Success,
            BackendAuthenticationMode.DirectApiKey => ValidateDirectApiKeyMode(),
            _ => ValidateOptionsResult.Fail("The backend authentication mode is not supported.")
        };
    }

    private ValidateOptionsResult ValidateDirectApiKeyMode()
    {
        if (!_environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail("Direct API-key authentication is permitted only in the Development environment.");
        }

        if (string.IsNullOrWhiteSpace(_configuration[$"{ApiKeyOptions.SectionName}:{nameof(ApiKeyOptions.ApiKey)}"]))
        {
            return ValidateOptionsResult.Fail("Direct API-key authentication requires a configured API key.");
        }

        return ValidateOptionsResult.Success;
    }
}
