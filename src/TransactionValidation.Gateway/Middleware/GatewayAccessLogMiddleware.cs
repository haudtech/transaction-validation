using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using TransactionValidation.Gateway.Errors;
using TransactionValidation.Gateway.HealthChecks;

using Yarp.ReverseProxy.Model;

namespace TransactionValidation.Gateway.Middleware;

/// <summary>
/// Emits one metadata-only access/security log entry per request. Credentials, query values, bodies and paths are never logged.
/// </summary>
public sealed partial class GatewayAccessLogMiddleware(
    RequestDelegate next,
    ILogger<GatewayAccessLogMiddleware> logger)
{
    private const string UnmatchedRoute = "unmatched";
    private const string UnknownDecision = "unknown";

    public async Task InvokeAsync(HttpContext context)
    {
        if (GatewayHealthPaths.IsHealthProbe(context.Request.Path))
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            Log(context, Stopwatch.GetElapsedTime(started));
        }
    }

    private void Log(HttpContext context, TimeSpan elapsed)
    {
        var routeId = context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.RouteId ?? UnmatchedRoute;
        var decision = GatewayRequestMetadata.GetDecision(context) ?? UnknownDecision;
        var api = GatewayRequestMetadata.GetApi(context);
        GatewayAuthenticationMiddleware.TryGetCredential(context, out var credential);
        var sourceIp = context.Connection.RemoteIpAddress?.ToString();
        var correlationId = GatewayCorrelationMiddleware.GetCorrelationId(context);
        var status = context.Response.StatusCode;
        var latencyMs = elapsed.TotalMilliseconds;

        if (credential is null)
        {
            // Unauthenticated requests log only outcome, source IP, correlation ID and route.
            LogUnauthenticated(logger, routeId, status, decision, sourceIp, correlationId);
            return;
        }

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogAuthenticatedFailure(logger, routeId, api?.ApiId, api?.ApiVersion, credential.ClientId, credential.KeyId, status, decision, sourceIp, correlationId, latencyMs);
            return;
        }

        LogAuthenticated(logger, routeId, api?.ApiId, api?.ApiVersion, credential.ClientId, credential.KeyId, status, decision, sourceIp, correlationId, latencyMs);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Gateway request RouteId={RouteId} Status={Status} Decision={Decision} SourceIp={SourceIp} CorrelationId={CorrelationId}")]
    private static partial void LogUnauthenticated(
        ILogger logger, string routeId, int status, string decision, string? sourceIp, string? correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Gateway request RouteId={RouteId} ApiId={ApiId} ApiVersion={ApiVersion} ClientId={ClientId} KeyId={KeyId} Status={Status} Decision={Decision} SourceIp={SourceIp} CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogAuthenticated(
        ILogger logger, string routeId, string? apiId, string? apiVersion, string clientId, string keyId, int status, string decision, string? sourceIp, string? correlationId, double latencyMs);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Gateway request RouteId={RouteId} ApiId={ApiId} ApiVersion={ApiVersion} ClientId={ClientId} KeyId={KeyId} Status={Status} Decision={Decision} SourceIp={SourceIp} CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogAuthenticatedFailure(
        ILogger logger, string routeId, string? apiId, string? apiVersion, string clientId, string keyId, int status, string decision, string? sourceIp, string? correlationId, double latencyMs);
}
