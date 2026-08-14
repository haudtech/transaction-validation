using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TransactionValidation.Configuration.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddTransactionValidationCommonServices(builder.Configuration);

var app = builder.Build();

app.UseTransactionValidationCommon();
app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation API"));

app.Run();
