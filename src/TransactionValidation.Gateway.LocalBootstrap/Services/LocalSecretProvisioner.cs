using System.Security.Cryptography;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Secrets.Options;

namespace TransactionValidation.Gateway.LocalBootstrap.Services;

public sealed class LocalSecretProvisioner(
    ISecretProvider secretProvider,
    ISecretWriter secretWriter,
    IOptions<SecretProviderOptions> secretOptions,
    IHostEnvironment environment)
{
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public void PrepareDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Local bootstrap requires Unix file permissions.");
        }

        var directory = Path.GetFullPath(secretOptions.Value.Directory, environment.ContentRootPath);
        Directory.CreateDirectory(directory, PrivateDirectoryMode);
        // A pre-existing bind mount is usually group/other readable, which the provider rejects.
        File.SetUnixFileMode(directory, PrivateDirectoryMode);
    }

    public async Task<bool> ExistsAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            var secret = await secretProvider.GetSecretAsync(secretName, cancellationToken);
            CryptographicOperations.ZeroMemory(secret);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public async Task EnsurePepperAsync(string secretName, CancellationToken cancellationToken)
    {
        if (await ExistsAsync(secretName, cancellationToken))
        {
            return;
        }

        var pepper = RandomNumberGenerator.GetBytes(32);
        try
        {
            await secretWriter.WriteSecretAsync(secretName, pepper, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pepper);
        }
    }
}
