namespace TransactionValidation.Gateway.Contracts.Models;

/// <summary>
/// Conventions shared by Gateway-generated and API-generated RFC 7807 responses.
/// </summary>
public static class GatewayProblemConventions
{
    public const string CorrelationHeaderName = "Correlation-Id";
    public const string TypeBaseUri = "https://httpstatuses.com/";
    public const string CodeExtension = "code";
    public const string CorrelationIdExtension = "correlationId";
}
