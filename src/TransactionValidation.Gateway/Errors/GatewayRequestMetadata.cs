using Microsoft.AspNetCore.Http;

namespace TransactionValidation.Gateway.Errors;

/// <summary>
/// Per-request facts recorded by the pipeline for the access log; values never contain credentials.
/// </summary>
internal static class GatewayRequestMetadata
{
    public const string Forwarded = "forwarded";

    private const string DecisionKey = "TransactionValidation.GatewayDecision";
    private const string ApiKey = "TransactionValidation.GatewayApi";

    public static void SetDecision(HttpContext context, string decision) =>
        context.Items[DecisionKey] = decision;

    public static string? GetDecision(HttpContext context) =>
        context.Items.TryGetValue(DecisionKey, out var value) ? value as string : null;

    public static void SetApi(HttpContext context, string apiId, string apiVersion) =>
        context.Items[ApiKey] = (apiId, apiVersion);

    public static (string ApiId, string ApiVersion)? GetApi(HttpContext context) =>
        context.Items.TryGetValue(ApiKey, out var value) && value is (string id, string version)
            ? (id, version)
            : null;
}
