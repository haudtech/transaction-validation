namespace TransactionValidation.Gateway.Contracts;

public interface IGatewayCredentialVerifier
{
    bool IsValid(string credential);
}
