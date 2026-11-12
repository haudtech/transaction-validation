using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Options;

namespace TransactionValidation.Gateway.Validators;

public sealed class GatewayRuntimeOptionsValidator : IValidateOptions<GatewayRuntimeOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayRuntimeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretsDirectory))
        {
            return ValidateOptionsResult.Fail("Gateway:SecretsDirectory is required.");
        }

        if (!IsSafeSecretName(options.BackendCredentialSecretName))
        {
            return ValidateOptionsResult.Fail(
                "Gateway:BackendCredentialSecretName must be a safe local secret filename.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsSafeSecretName(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 64
        && IsAsciiAlphaNumeric(value[0])
        && value.All(character => IsAsciiAlphaNumeric(character) || character is '-' or '_');

    private static bool IsAsciiAlphaNumeric(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9';
}
