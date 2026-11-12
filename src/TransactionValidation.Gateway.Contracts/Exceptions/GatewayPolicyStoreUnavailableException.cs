namespace TransactionValidation.Gateway.Contracts.Exceptions;

public sealed class GatewayPolicyStoreUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
