using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TransactionValidation.Gateway.LocalBootstrap.Options;
using TransactionValidation.Gateway.LocalBootstrap.Services;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.Management.Services;
using TransactionValidation.Gateway.PolicyStore.Extensions;
using TransactionValidation.Gateway.Secrets.Extensions;

var builder = Host.CreateApplicationBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    await Console.Error.WriteLineAsync("Local gateway bootstrap runs only in the Development environment.");
    return 2;
}

builder.Services.AddOptions<LocalBootstrapOptions>()
    .Bind(builder.Configuration.GetSection(LocalBootstrapOptions.SectionName))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientKeyId)
            && !string.IsNullOrWhiteSpace(options.ClientPepperVersion)
            && !string.IsNullOrWhiteSpace(options.CredentialSecretName),
        "LocalBootstrap client, key, pepper, and credential names are required.")
    .Validate(options =>
            options.RatePerMinute > 0 && options.Burst > 0 && options.DailyQuota > 0
            && options.MonthlyQuota >= options.DailyQuota,
        "LocalBootstrap limits must be finite and positive, and the monthly quota must not be below the daily quota.")
    .ValidateOnStart();
builder.Services.AddOptions<BackendCredentialManagementOptions>()
    .Bind(builder.Configuration.GetSection(BackendCredentialManagementOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.PepperVersion),
        "Gateway:BackendCredentialManagement:PepperVersion is required.")
    .ValidateOnStart();
builder.Services.AddGatewayPolicyStore(builder.Configuration);
builder.Services.AddConfiguredSecretProvider(builder.Configuration, builder.Environment, requireWriter: true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalSecretProvisioner>();
builder.Services.AddScoped<GatewayBackendCredentialManagementService>();
builder.Services.AddScoped<LocalClientSeeder>();
builder.Services.AddScoped<LocalGatewayBootstrapper>();

using var host = builder.Build();
try
{
    await host.StartAsync();
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LocalGatewayBootstrapper>().RunAsync(CancellationToken.None);
    await host.StopAsync();
    return 0;
}
catch (Exception exception)
{
    // Exit explicitly so Compose sees a failed one-shot service instead of a hung process.
    await Console.Error.WriteLineAsync($"Local gateway bootstrap failed: {exception.GetType().Name}: {exception.Message}");
    return 1;
}
