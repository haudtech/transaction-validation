using System.Globalization;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.Middleware;

namespace TransactionValidation.Gateway.Errors;

/// <summary>
/// Writes sanitized RFC 7807 responses for errors the Gateway itself produces.
/// </summary>
internal static class GatewayProblemWriter
{
    public static Task WriteAsync(HttpContext context, int statusCode, string code, string detail)
    {
        GatewayRequestMetadata.SetDecision(context, code);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Type = string.Create(CultureInfo.InvariantCulture, $"{GatewayProblemConventions.TypeBaseUri}{statusCode}")
        };
        problem.Extensions[GatewayProblemConventions.CodeExtension] = code;
        var correlationId = GatewayCorrelationMiddleware.GetCorrelationId(context);
        if (correlationId is not null)
        {
            problem.Extensions[GatewayProblemConventions.CorrelationIdExtension] = correlationId;
        }

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            problem,
            (JsonSerializerOptions?)null,
            "application/problem+json",
            context.RequestAborted);
    }
}
