using TransactionValidation.Gateway.Secrets.Providers;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class LocalFileSecretProviderTests
{
    [Fact]
    public async Task GetSecretAsync_ReadsNamedSecretFromConfiguredDirectory()
    {
        var root = CreateSecureDirectory();
        try
        {
            var expected = new byte[] { 1, 2, 3, 4 };
            WritePrivateFile(root, "pepper-v1", expected);
            var provider = new LocalFileSecretProvider(root);

            if (OperatingSystem.IsWindows())
            {
                await Assert.ThrowsAsync<PlatformNotSupportedException>(
                    () => provider.GetSecretAsync("pepper-v1", CancellationToken.None).AsTask());
                return;
            }

            var actual = await provider.GetSecretAsync("pepper-v1", CancellationToken.None);

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("pepper/v1")]
    [InlineData(".")]
    [InlineData("")]
    public async Task GetSecretAsync_RejectsUnsafeSecretNames(string secretName)
    {
        var provider = new LocalFileSecretProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetSecretAsync(secretName, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsWhenSecretDoesNotExist()
    {
        var root = CreateSecureDirectory();
        try
        {
            var provider = new LocalFileSecretProvider(root);

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => provider.GetSecretAsync("missing", CancellationToken.None).AsTask());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteSecretAsync_CreatesPrivateSecretFileThatCanBeReadBack()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var provider = new LocalFileSecretProvider(root);
        var secret = new byte[] { 9, 8, 7 };
        try
        {
            await provider.WriteSecretAsync("issued-key", secret, CancellationToken.None);

            Assert.Equal(secret, await provider.GetSecretAsync("issued-key", CancellationToken.None));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(root, "issued-key")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteSecretAsync_DoesNotOverwriteAnExistingSecret()
    {
        var root = CreateSecureDirectory();
        try
        {
            WritePrivateFile(root, "issued-key", [1]);
            var provider = new LocalFileSecretProvider(root);

            await Assert.ThrowsAsync<IOException>(
                () => provider.WriteSecretAsync("issued-key", new byte[] { 2 }, CancellationToken.None).AsTask());
            Assert.Equal([1], await provider.GetSecretAsync("issued-key", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetSecretAsync_RejectsFilesReadableByGroupOrOtherUsers()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateSecureDirectory();
        try
        {
            var path = Path.Combine(root, "pepper-v1");
            await File.WriteAllBytesAsync(path, [1, 2, 3]);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead);
            var provider = new LocalFileSecretProvider(root);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetSecretAsync("pepper-v1", CancellationToken.None).AsTask());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetSecretAsync_RejectsDirectoriesAccessibleByGroupOrOtherUsers()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateSecureDirectory();
        try
        {
            WritePrivateFile(root, "pepper-v1", [1, 2, 3]);
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead);
            var provider = new LocalFileSecretProvider(root);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetSecretAsync("pepper-v1", CancellationToken.None).AsTask());
        }
        finally
        {
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetSecretAsync_RejectsSymbolicLinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateSecureDirectory();
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await File.WriteAllBytesAsync(outsidePath, [1, 2, 3]);
            File.SetUnixFileMode(outsidePath, UnixFileMode.UserRead);
            File.CreateSymbolicLink(Path.Combine(root, "pepper-v1"), outsidePath);
            var provider = new LocalFileSecretProvider(root);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetSecretAsync("pepper-v1", CancellationToken.None).AsTask());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(outsidePath);
        }
    }

    private static string CreateSecureDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    private static void WritePrivateFile(string root, string name, byte[] contents)
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, contents);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
