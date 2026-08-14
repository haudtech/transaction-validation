using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using DotNetEnv;

namespace TransactionValidation.Configuration.Extensions;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddTransactionValidationConfiguration(
        this IConfigurationBuilder configuration,
        IHostEnvironment hostEnvironment,
        string[] args)
    {
        var environmentName = ResolveEnvironmentName(hostEnvironment.EnvironmentName);

        configuration.Sources.Clear();
        configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddDotEnvIfExists(hostEnvironment.ContentRootPath)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        return configuration;
    }

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