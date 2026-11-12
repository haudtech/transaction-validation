using TransactionValidation.Gateway.Extensions;
using TransactionValidation.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddGatewayAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.UseMiddleware<GatewayCorrelationMiddleware>();
app.UseMiddleware<GatewayAuthenticationMiddleware>();
app.UseMiddleware<GatewayAuthorizationMiddleware>();
app.MapReverseProxy();

app.Run();
