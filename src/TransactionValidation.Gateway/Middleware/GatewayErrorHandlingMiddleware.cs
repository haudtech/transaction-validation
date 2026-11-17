using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using TransactionValidation.Gateway.Errors;

using Yarp.ReverseProxy.Forwarder;

namespace TransactionValidation.Gateway.Middleware;

/// <summary>
/// Converts Gateway-originated failures into sanitized problem responses; backend responses pass through unchanged.
/// </summary>
public sealed class GatewayErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<GatewayErrorHandlingMiddleware> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledExceptionMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(0, nameof(LogUnhandledException)),
            "Unhandled gateway exception of type {ExceptionType}.");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException && !context.Response.HasStarted)
        {
            if (exception is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
            {
                await WritePayloadTooLargeAsync(context);
                return;
            }

            LogUnhandledException(logger, exception.GetType().Name);
            context.Response.Clear();
            await GatewayProblemWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                GatewayErrorCodes.InternalError,
                "The gateway could not process the request.");
            return;
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var forwarderError = context.Features.Get<IForwarderErrorFeature>();
        if (forwarderError is null)
        {
            return;
        }

        if (forwarderError.Exception is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            await WritePayloadTooLargeAsync(context);
            return;
        }

        switch (context.Response.StatusCode)
        {
            case StatusCodes.Status504GatewayTimeout:
                await GatewayProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status504GatewayTimeout,
                    GatewayErrorCodes.UpstreamTimeout,
                    "The upstream service did not respond in time.");
                break;
            case StatusCodes.Status502BadGateway:
                await GatewayProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status502BadGateway,
                    GatewayErrorCodes.UpstreamUnavailable,
                    "The upstream service is unavailable.");
                break;
            default:
                break;
        }
    }

    private static Task WritePayloadTooLargeAsync(HttpContext context)
    {
        context.Response.Clear();
        return GatewayProblemWriter.WriteAsync(
            context,
            StatusCodes.Status413PayloadTooLarge,
            GatewayErrorCodes.PayloadTooLarge,
            "The request body is too large.");
    }

    private static void LogUnhandledException(ILogger logger, string exceptionType) =>
        LogUnhandledExceptionMessage(logger, exceptionType, null);
}
