using Microsoft.AspNetCore.Authentication.JwtBearer;

using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Management.Endpoints;
using TransactionValidation.Gateway.Management.Hosting;
using TransactionValidation.Gateway.Management.Options;
using TransactionValidation.Gateway.Management.Services;
using TransactionValidation.Gateway.PolicyStore.Extensions;
using TransactionValidation.Gateway.Secrets.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOptions<ManagementOidcOptions>()
    .Bind(builder.Configuration.GetSection(ManagementOidcOptions.SectionName))
    .Validate(options =>
            Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
            && authority.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrWhiteSpace(options.Audience)
            && !string.IsNullOrWhiteSpace(options.ReadScope)
            && !string.IsNullOrWhiteSpace(options.WriteScope)
            && !string.Equals(options.ReadScope, options.WriteScope, StringComparison.Ordinal),
        "Valid HTTPS OIDC authority, audience, and management scopes are required.")
    .ValidateOnStart();
builder.Services.AddOptions<BackendCredentialManagementOptions>()
    .Bind(builder.Configuration.GetSection(BackendCredentialManagementOptions.SectionName))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.PepperVersion)
            && options.PepperVersion.Length <= 64
            && IsSafeSecretName(options.PepperVersion),
        "A safe backend credential pepper version is required.")
    .ValidateOnStart();
builder.Services.AddGatewayPolicyStore(builder.Configuration);
builder.Services.AddConfiguredSecretProvider(builder.Configuration, builder.Environment, requireWriter: true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GatewayBackendCredentialManagementService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration[$"{ManagementOidcOptions.SectionName}:Authority"];
        options.Audience = builder.Configuration[$"{ManagementOidcOptions.SectionName}:Audience"];
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.Services.GetRequiredService<ISecretWriter>();
app.MapHealthChecks("/healthz");
app.UseAuthentication();
app.UseAuthorization();
app.MapGatewayBackendCredentialEndpoints(
    app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementOidcOptions>>().Value);

app.Run();

static bool IsSafeSecretName(string value) =>
    value.Length > 0
    && value[0] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
    && value.All(character =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_');
