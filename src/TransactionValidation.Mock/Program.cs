using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Configuration.Extensions;
using TransactionValidation.Mock.Options;
using TransactionValidation.Mock.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddTransactionValidationConfiguration(builder.Environment, args);

builder.Services.AddControllers();
builder.Services.Configure<RabbitMqPrimaryConsumerOptions>(
    builder.Configuration.GetSection(RabbitMqPrimaryConsumerOptions.SectionName));
builder.Services.Configure<RabbitMqAuditConsumerOptions>(
    builder.Configuration.GetSection(RabbitMqAuditConsumerOptions.SectionName));
builder.Services.AddSingleton<ConsumerObservationStore>();
builder.Services.AddSingleton<ConsumerFailureControl>();
if (builder.Configuration.GetValue<bool>($"{RabbitMqPrimaryConsumerOptions.SectionName}:Enabled"))
{
    builder.Services.AddHostedService<RabbitMqNoOpConsumerService>();
}

if (builder.Configuration.GetValue<bool>($"{RabbitMqAuditConsumerOptions.SectionName}:Enabled"))
{
    builder.Services.AddHostedService<RabbitMqAuditConsumerService>();
}

var app = builder.Build();
app.MapControllers();
app.MapGet("/", () => Results.Ok("TransactionValidation Mock"));
app.Run();
