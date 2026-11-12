using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.LocalBootstrap.Options;
using TransactionValidation.Gateway.LocalBootstrap.Services;
using TransactionValidation.Gateway.PolicyStore.Context;
using TransactionValidation.Gateway.Secrets.Options;
using TransactionValidation.Gateway.Secrets.Providers;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class LocalClientSeederTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tv-seeder-" + Guid.NewGuid().ToString("N"));
    private readonly string databaseName = Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_CreatesClientGrantKeyAndVerifiableCredential()
    {
        var (seeder, db, provider, settings) = Create();
        using var _ = db;

        Assert.True(await seeder.SeedAsync(CancellationToken.None));

        Assert.Single(db.Clients);
        Assert.Single(db.ClientApiGrants);
        var key = Assert.Single(db.ClientKeys);
        var credential = Encoding.UTF8.GetString(await provider.GetSecretAsync(settings.CredentialSecretName, CancellationToken.None));
        var parts = credential.Split('.');
        Assert.Equal(settings.ClientKeyId, parts[0]);
        Assert.Equal(43, parts[1].Length);
        var pepper = await provider.GetSecretAsync(settings.ClientPepperVersion, CancellationToken.None);
        Assert.Equal(key.Verifier, HMACSHA256.HashData(pepper, Encoding.ASCII.GetBytes(parts[1])));
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_IsIdempotent()
    {
        var (seeder, db, provider, settings) = Create();
        using var _ = db;
        await seeder.SeedAsync(CancellationToken.None);
        var first = await provider.GetSecretAsync(settings.CredentialSecretName, CancellationToken.None);

        Assert.False(await seeder.SeedAsync(CancellationToken.None));

        Assert.Single(db.ClientKeys);
        Assert.Equal(first, await provider.GetSecretAsync(settings.CredentialSecretName, CancellationToken.None));
    }

    [Fact]
    public async Task SeedAsync_WhenKeyExistsButCredentialMissing_Throws()
    {
        var (seeder, db, _, settings) = Create();
        using var _ = db;
        await seeder.SeedAsync(CancellationToken.None);
        File.Delete(Path.Combine(root, settings.CredentialSecretName));

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseResetButStaleCredentialExists_ReplacesCredential()
    {
        var (seeder, db, provider, settings) = Create();
        using var _ = db;
        await seeder.SeedAsync(CancellationToken.None);
        var stale = await provider.GetSecretAsync(settings.CredentialSecretName, CancellationToken.None);
        db.ClientKeys.RemoveRange(db.ClientKeys);
        await db.SaveChangesAsync();

        Assert.True(await seeder.SeedAsync(CancellationToken.None));

        Assert.NotEqual(stale, await provider.GetSecretAsync(settings.CredentialSecretName, CancellationToken.None));
    }

    [Fact]
    public void PrepareDirectory_TightensExistingDirectoryPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(root);
        File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var provisioner = CreateProvisioner(new LocalFileSecretProvider(root));

        provisioner.PrepareDirectory();

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(root));
    }

    private LocalSecretProvisioner CreateProvisioner(LocalFileSecretProvider provider)
        => new(
            provider,
            provider,
            Options.Create(new SecretProviderOptions { Provider = SecretProviderOptions.LocalFileProvider, Directory = root }),
            new TestEnvironment());

    private (LocalClientSeeder Seeder, GatewayPolicyDbContext Db, LocalFileSecretProvider Provider, LocalBootstrapOptions Settings) Create()
    {
        var provider = new LocalFileSecretProvider(root);
        var provisioner = CreateProvisioner(provider);
        provisioner.PrepareDirectory();
        var db = new GatewayPolicyDbContext(
            new DbContextOptionsBuilder<GatewayPolicyDbContext>().UseInMemoryDatabase(databaseName).Options);
        var settings = new LocalBootstrapOptions();
        var seeder = new LocalClientSeeder(db, provider, provider, provisioner, Options.Create(settings), TimeProvider.System);
        return (seeder, db, provider, settings);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "TransactionValidation.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
