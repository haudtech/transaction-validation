using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TransactionValidation.Api.Idempotency;
using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddTransactionValidationConfiguration(hostingContext.HostingEnvironment, args);
});

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransactionValidationCommonServices(builder.Configuration);

builder.Services.AddSingleton<IIdempotencyStore>(sp =>
{
    var options = sp.GetRequiredService<IdempotencyOptions>();
    var idempotencyWindowMinutes = Math.Clamp(options.WindowMinutes, 10, 15);
    return new InMemoryIdempotencyStore(TimeSpan.FromMinutes(idempotencyWindowMinutes));
});

var app = builder.Build();

app.UseTransactionValidationCommon();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation API"));

app.Run();

public partial class Program
{
}
