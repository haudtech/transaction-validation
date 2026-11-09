using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using TransactionValidation.Configuration.Options;
using TransactionValidation.Gateway.Contracts;

namespace TransactionValidation.Configuration.Middleware;

public sealed class ConfiguredGatewayCredentialVerifier : IGatewayCredentialVerifier
{
    private readonly string _configuredCredential;

    public ConfiguredGatewayCredentialVerifier(IOptions<BackendAuthenticationOptions> options)
    {
        _configuredCredential = options.Value.GatewayKey;
    }

    public bool IsValid(string credential)
    {
        if (string.IsNullOrEmpty(_configuredCredential))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(credential);
        var configuredBytes = Encoding.UTF8.GetBytes(_configuredCredential);

        try
        {
            return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(providedBytes);
            CryptographicOperations.ZeroMemory(configuredBytes);
        }
    }
}
