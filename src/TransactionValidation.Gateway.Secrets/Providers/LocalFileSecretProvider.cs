using TransactionValidation.Gateway.Contracts.Interfaces;

namespace TransactionValidation.Gateway.Secrets.Providers;

public sealed class LocalFileSecretProvider : ISecretProvider
{
    private const UnixFileMode InsecureFileModes =
        UnixFileMode.GroupRead
        | UnixFileMode.GroupWrite
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherWrite
        | UnixFileMode.OtherExecute;

    private readonly string rootDirectory;

    public LocalFileSecretProvider(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public async ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        if (!IsSafeSecretName(secretName))
        {
            throw new ArgumentException("Secret names may contain only ASCII letters, digits, hyphens, and underscores.", nameof(secretName));
        }

        var rootAttributes = File.GetAttributes(rootDirectory);
        if ((rootAttributes & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException("The configured local secret path must be a directory.");
        }

        if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The configured local secret directory must not be a symbolic link.");
        }

        EnsurePrivateDirectoryPermissions(rootDirectory);

        var secretPath = Path.Combine(rootDirectory, secretName);
        var secretAttributes = File.GetAttributes(secretPath);
        if ((secretAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Local secret files must not be symbolic links.");
        }

        if ((secretAttributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidDataException($"The local secret path '{secretName}' must be a file.");
        }

        EnsurePrivateFilePermissions(secretPath);

        var secret = await File.ReadAllBytesAsync(secretPath, cancellationToken);
        if (secret.Length == 0)
        {
            throw new InvalidDataException($"The local secret file '{secretName}' is empty.");
        }

        return secret;
    }

    private static bool IsSafeSecretName(string secretName)
    {
        if (secretName.Length > 64 || !IsAsciiAlphaNumeric(secretName[0]))
        {
            return false;
        }

        return secretName.All(character =>
            IsAsciiAlphaNumeric(character) || character is '-' or '_');
    }

    private static bool IsAsciiAlphaNumeric(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9';

    private static void EnsurePrivateFilePermissions(string secretPath)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The local file secret provider requires Unix permission validation and is not supported on Windows.");
        }

        var mode = File.GetUnixFileMode(secretPath);
        if ((mode & UnixFileMode.UserRead) == 0 || (mode & InsecureFileModes) != 0)
        {
            throw new InvalidOperationException("Local secret files must be owner-readable and inaccessible to group and other users.");
        }
    }

    private static void EnsurePrivateDirectoryPermissions(string directoryPath)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The local file secret provider requires Unix permission validation and is not supported on Windows.");
        }

        var mode = File.GetUnixFileMode(directoryPath);
        if ((mode & (UnixFileMode.UserRead | UnixFileMode.UserExecute))
                != (UnixFileMode.UserRead | UnixFileMode.UserExecute)
            || (mode & InsecureFileModes) != 0)
        {
            throw new InvalidOperationException(
                "The local secret directory must be owner-readable and searchable, and inaccessible to group and other users.");
        }
    }
}
