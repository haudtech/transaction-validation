using DotNetEnv;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TransactionValidation.Configuration.Extensions;

/// <summary>
/// Loads the application configuration in the precedence order described by the design docs: appsettings, environment-specific appsettings, dot-env files, environment variables, and command-line arguments.
/// This keeps runtime settings consistent across local development and container-based execution.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Configures the application using the standard layered precedence defined by the design docs: local settings, environment-specific settings, dot-env files, environment variables, and command-line arguments.
    /// </summary>
    /// <param name="configuration">The configuration builder being initialized.</param>
    /// <param name="hostEnvironment">The hosting environment used to resolve the active environment name.</param>
    /// <param name="args">Command-line arguments to append to the configuration pipeline.</param>
    /// <returns>The same configuration builder instance with the TransactionValidation settings loaded.</returns>
    public static IConfigurationBuilder AddTransactionValidationConfiguration(
        this IConfigurationBuilder configuration,
        IHostEnvironment hostEnvironment,
        string[] args)
    {
        // Load .env before resolving the environment so it can select the environment-specific JSON file.
        AddDotEnvIfExists(configuration, hostEnvironment.ContentRootPath);
        var environmentName = ResolveEnvironmentName(hostEnvironment.EnvironmentName);

        configuration.Sources.Clear();
        configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        return configuration;
    }

    /// <summary>
    /// Resolves the active environment from the standard ASP.NET variable or the legacy EVN override.
    /// </summary>
    /// <param name="fallbackEnvironmentName">The environment name supplied by the host when no override is set.</param>
    /// <returns>The effective runtime environment name.</returns>
    private static string ResolveEnvironmentName(string fallbackEnvironmentName)
    {
        var evn = Environment.GetEnvironmentVariable("EVN");
        if (!string.IsNullOrWhiteSpace(evn))
        {
            return evn.Trim();
        }

        var aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(aspNetCoreEnvironment))
        {
            return aspNetCoreEnvironment.Trim();
        }

        return fallbackEnvironmentName;
    }

    /// <summary>
    /// Loads a local .env file if it exists in the app root or parent directories so local configuration can be injected without committing secrets.
    /// </summary>
    /// <param name="configuration">The builder being populated.</param>
    /// <param name="contentRootPath">Application root used to search for environment files.</param>
    /// <returns>The same configuration builder instance.</returns>
    private static IConfigurationBuilder AddDotEnvIfExists(this IConfigurationBuilder configuration, string contentRootPath)
    {
        foreach (var envFile in EnumerateDotEnvCandidates(contentRootPath))
        {
            if (File.Exists(envFile))
            {
                // Preserve pre-existing process values (for example ASPNETCORE_ENVIRONMENT from host).
                Env.NoClobber().Load(envFile);
                break;
            }
        }

        return configuration;
    }

    /// <summary>
    /// Enumerates likely .env file locations starting from the application root and walking upward a few directories.
    /// </summary>
    /// <param name="contentRootPath">The content root to search from.</param>
    /// <returns>A list of candidate .env paths ordered from nearest to farthest ancestor.</returns>
    private static IEnumerable<string> EnumerateDotEnvCandidates(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        var candidates = new List<string>();

        for (var i = 0; i < 3 && current is not null; i++)
        {
            candidates.Add(Path.Combine(current.FullName, ".env"));
            current = current.Parent;
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
