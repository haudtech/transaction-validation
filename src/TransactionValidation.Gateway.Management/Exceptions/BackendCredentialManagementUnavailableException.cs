namespace TransactionValidation.Gateway.Management.Exceptions;

public sealed class BackendCredentialManagementUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
