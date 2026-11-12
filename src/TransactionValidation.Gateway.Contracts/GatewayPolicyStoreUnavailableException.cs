namespace TransactionValidation.Gateway.Contracts;

public sealed class GatewayPolicyStoreUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
