using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using TransactionValidation.Gateway.Errors;

using Yarp.ReverseProxy.Forwarder;

namespace TransactionValidation.Gateway.Middleware;

/// <summary>
/// Converts Gateway-originated failures into sanitized problem responses; backend responses pass through unchanged.
/// </summary>
public sealed partial class GatewayErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<GatewayErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Kestrel enforces the limit only on body reads, and a backend can answer before the proxy reads it all.
            var maxBodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
            if (maxBodySize is not null && context.Request.ContentLength > maxBodySize)
            {
                await WritePayloadTooLargeAsync(context);
                return;
            }

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled gateway exception of type {ExceptionType}.")]
    private static partial void LogUnhandledException(ILogger logger, string exceptionType);
}
