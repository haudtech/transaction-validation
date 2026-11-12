using TransactionValidation.Gateway.Contracts.Interfaces;

namespace TransactionValidation.Gateway.Secrets.Providers;

public sealed class LocalFileSecretProvider : ISecretProvider, ISecretWriter
{
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

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

    public async ValueTask WriteSecretAsync(
        string secretName,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken)
    {
        ValidateSecretName(secretName);
        if (secret.IsEmpty)
        {
            throw new ArgumentException("Secrets cannot be empty.", nameof(secret));
        }

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The local file secret provider requires Unix permission validation and is not supported on Windows.");
        }

        EnsurePrivateRootDirectory();
        var destinationPath = Path.Combine(rootDirectory, secretName);
        var temporaryPath = Path.Combine(rootDirectory, $".{secretName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                    UnixCreateMode = PrivateFileMode
                }))
            {
                await stream.WriteAsync(secret, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ValueTask DeleteSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSecretName(secretName);
        EnsurePrivateRootDirectory();
        var secretPath = Path.Combine(rootDirectory, secretName);
        if (File.Exists(secretPath))
        {
            var attributes = File.GetAttributes(secretPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidOperationException("Local secret paths must be regular files.");
            }

            File.Delete(secretPath);
        }

        return ValueTask.CompletedTask;
    }

    private void EnsurePrivateRootDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The local file secret provider requires Unix permission validation and is not supported on Windows.");
        }

        if (!Directory.Exists(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory, PrivateDirectoryMode);
        }

        var attributes = File.GetAttributes(rootDirectory);
        if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The configured local secret path must be a non-symbolic-link directory.");
        }

        EnsurePrivateDirectoryPermissions(rootDirectory);
    }

    private static void ValidateSecretName(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        if (!IsSafeSecretName(secretName))
        {
            throw new ArgumentException(
                "Secret names may contain only ASCII letters, digits, hyphens, and underscores.",
                nameof(secretName));
        }
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
