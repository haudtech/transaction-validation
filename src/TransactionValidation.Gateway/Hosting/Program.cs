using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using TransactionValidation.Gateway.Extensions;
using TransactionValidation.Gateway.HealthChecks;
using TransactionValidation.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Kestrel does not bind its Limits section from configuration automatically.
builder.WebHost.ConfigureKestrel((context, options) =>
    context.Configuration.GetSection("Kestrel:Limits").Bind(options.Limits));

builder.Services.AddHealthChecks()
    .AddCheck<GatewayPolicyHealthCheck>("gateway-policy", tags: ["ready"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<GatewayRedisHealthCheck>("gateway-redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddGatewayAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks(GatewayHealthPaths.Legacy, new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(GatewayHealthPaths.Live, new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(GatewayHealthPaths.Ready, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.UseMiddleware<GatewayCorrelationMiddleware>();
app.UseMiddleware<GatewayAccessLogMiddleware>();
app.UseMiddleware<GatewayErrorHandlingMiddleware>();
app.UseMiddleware<GatewayAuthenticationMiddleware>();
app.UseMiddleware<GatewayAuthorizationMiddleware>();
app.MapReverseProxy();

app.Run();
