using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Secrets.Options;
using TransactionValidation.Gateway.Secrets.Providers;
using TransactionValidation.Gateway.Secrets.Validators;

namespace TransactionValidation.Gateway.Secrets.Extensions;

public static class SecretProviderServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredSecretProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool requireWriter = false)
    {
        services.AddOptions<SecretProviderOptions>()
            .Bind(configuration.GetSection(SecretProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SecretProviderOptions>, SecretProviderOptionsValidator>();
        services.AddSingleton<ISecretProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SecretProviderOptions>>().Value;
            var rootDirectory = Path.GetFullPath(options.Directory, environment.ContentRootPath);
            return options.Provider switch
            {
                SecretProviderOptions.LocalFileProvider => new LocalFileSecretProvider(rootDirectory),
                _ => throw new InvalidOperationException(
                    $"Secret provider '{options.Provider}' is not registered.")
            };
        });

        if (requireWriter)
        {
            services.AddSingleton<ISecretWriter>(serviceProvider =>
            {
                var providerName = serviceProvider.GetRequiredService<IOptions<SecretProviderOptions>>().Value.Provider;
                return serviceProvider.GetRequiredService<ISecretProvider>() is ISecretWriter writer
                    ? writer
                    : throw new InvalidOperationException(
                        $"Secret provider '{providerName}' does not support secret writes.");
            });
        }

        return services;
    }
}
