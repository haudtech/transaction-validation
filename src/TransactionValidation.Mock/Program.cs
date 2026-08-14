using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Mock.Options;
using TransactionValidation.Mock.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
	config.AddTransactionValidationConfiguration(hostingContext.HostingEnvironment, args);
});

builder.Services.AddControllers();
builder.Services.Configure<RabbitMqConsumerOptions>(
	builder.Configuration.GetSection(RabbitMqConsumerOptions.SectionName));
builder.Services.AddHostedService<RabbitMqNoOpConsumerService>();

var app = builder.Build();
app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation Mock"));
app.Run();
