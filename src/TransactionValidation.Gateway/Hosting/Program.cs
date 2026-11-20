using TransactionValidation.Gateway.Extensions;
using TransactionValidation.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Kestrel does not bind its Limits section from configuration automatically.
builder.WebHost.ConfigureKestrel((context, options) =>
    context.Configuration.GetSection("Kestrel:Limits").Bind(options.Limits));

builder.Services.AddHealthChecks();
builder.Services.AddGatewayAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.UseMiddleware<GatewayCorrelationMiddleware>();
app.UseMiddleware<GatewayAccessLogMiddleware>();
app.UseMiddleware<GatewayErrorHandlingMiddleware>();
app.UseMiddleware<GatewayAuthenticationMiddleware>();
app.UseMiddleware<GatewayAuthorizationMiddleware>();
app.MapReverseProxy();

app.Run();
