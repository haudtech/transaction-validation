namespace TransactionValidation.Gateway.Contracts.Exceptions;

public sealed class GatewayRequestLimiterUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
