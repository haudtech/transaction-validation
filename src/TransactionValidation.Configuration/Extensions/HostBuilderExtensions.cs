using Microsoft.Extensions.Hosting;

using Serilog;

namespace TransactionValidation.Configuration.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder UseTransactionValidationSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, _, loggerConfiguration) =>
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext());
    }
}